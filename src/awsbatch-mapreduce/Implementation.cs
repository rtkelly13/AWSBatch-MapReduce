using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Amazon.Batch;
using Amazon.Batch.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Newtonsoft.Json;
using NodaTime;

namespace awsbatch_mapreduce;

public static class Implementation
{
    private static readonly YearMonth Min = new(2009, 1);
    private static readonly YearMonth Max = new(2010, 12);
    private const string Url = "https://d37ci6vzurychx.cloudfront.net/trip-data/yellow_tripdata_{0}.parquet";

    private static IEnumerable<YearMonth> LoadAllDates()
    {
        var current = Min;
        while (current <= Max)
        {
            yield return current;
            current = current.PlusMonths(1);
        }
    }

    public static async Task<int> SetupJob()
    {
        using var s3Client = new AmazonS3Client();
        using var batchClient = new AmazonBatchClient();
        
        var files = LoadAllDates().Select(x =>
        {
            var ym = x.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return new TaxiData
            {
                FileUrl = string.Format(Url, ym),
                YearMonth = ym
            };
        }).ToList();
        
        var json = JsonConvert.SerializeObject(files);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await S3.UploadFile(s3Client, ms, "jobData.json");

        var mapJobResponse = await batchClient.SubmitJobAsync(new SubmitJobRequest
        {
            JobName = "MapJob",
            JobDefinition = $"{Constants.JobName}-map",
            JobQueue = Constants.JobQueue,
            ArrayProperties = new ArrayProperties
            {
                Size = files.Count ,
                //Size = 2,
            },
            Parameters = new Dictionary<string, string>()
        });

        var reduceMapJob = await batchClient.SubmitJobAsync(new SubmitJobRequest
        {
            JobName = "ReduceJob",
            JobDefinition = $"{Constants.JobName}-reduce",
            JobQueue = Constants.JobQueue,
            Parameters = new Dictionary<string, string>(),
            DependsOn = new List<JobDependency>
            {
                new()
                {
                    JobId = mapJobResponse.JobId
                }
            }
        });

        return 0;
    }
}