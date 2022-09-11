using Amazon.S3;
using Amazon.S3.Transfer;

namespace awsbatch_mapreduce;

public static class S3
{
    public static async Task<MemoryStream> LoadFile(AmazonS3Client s3Client, string key)
    {
        using var fileS3Upload = new MemoryStream();
        var fileTransferUtility =
            new TransferUtility(s3Client);

        await using var fileOutput = await fileTransferUtility.OpenStreamAsync(Constants.BucketName, key);
        var ms = new MemoryStream();

        await fileOutput.CopyToAsync(ms);
        return ms;
    }

    public static async Task UploadFile(AmazonS3Client s3Client, MemoryStream file, string key)
    {
        using var fileS3Upload = new MemoryStream();
        await file.CopyToAsync(fileS3Upload);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileS3Upload,
            Key = key,
            BucketName = Constants.BucketName,
            CannedACL = S3CannedACL.Private
        };

        var fileTransferUtility = new TransferUtility(s3Client);
        await fileTransferUtility.UploadAsync(uploadRequest);
    }
}