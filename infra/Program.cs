using System;
using Pulumi;
using Pulumi.Aws.Ec2;
using System.Collections.Generic;
using awsbatch_mapreduce;
using Newtonsoft.Json;
using Pulumi.Aws;
using Pulumi.Aws.Batch;
using Pulumi.Aws.Batch.Inputs;
using Pulumi.Aws.Batch.Outputs;
using Pulumi.Aws.Ec2.Inputs;
using Pulumi.Aws.Ecr;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Iam.Inputs;
using Pulumi.Aws.S3;
// ReSharper disable ObjectCreationAsStatement

DefaultSubnet GetDefaultSubnet(char az) =>
    new($"defaultAz{az}", new()
    {
        AvailabilityZone = $"eu-west-1{az}",
        Tags =
        {
            { "Name", $"Default subnet for eu-west-1{az}" }
        },
    });

string AssumeInstancePolicy(string serviceName) =>
    JsonConvert.SerializeObject(new
    {
        Version = "2012-10-17",
        Statement = new []
        {
            new
            {
                Action = "sts:AssumeRole",
                Effect = "Allow",
                Principal = new
                {
                    Service = serviceName
                }
            }
        }
    }, Formatting.Indented);


return await Deployment.RunAsync(async () =>
{
    var jobName = Constants.JobName;  
    var caller = await GetCallerIdentity.InvokeAsync();
    
    // Create an AWS resource (S3 Bucket)
    var bucketName = $"{jobName}-bucket";
    var bucket = new Bucket(bucketName, new()
    {
        BucketName = bucketName
    });
    
    var defaultVpc = new DefaultVpc("default", new()
    {
        Tags =
       {
           { "Name", "Default VPC" },
       },
    });

    var assumePolicyEc2 = AssumeInstancePolicy("ec2.amazonaws.com");
    var ecsInstanceRoleRole = new Role("ecsInstanceRoleRole", new()
    {
        Name = "ecsInstanceRoleRole",
        AssumeRolePolicy = assumePolicyEc2,
    });

    new RolePolicyAttachment("ecsInstanceRoleRolePolicyAttachment", new()
    {
        Role = ecsInstanceRoleRole.Name,
        PolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonEC2ContainerServiceforEC2Role",
    });

    var ecsInstanceRoleInstanceProfile = new InstanceProfile("ecsInstanceRoleInstanceProfile", new()
    {
        Role = ecsInstanceRoleRole.Name,
    });

    var assumeRolePolicyBatch = AssumeInstancePolicy("batch.amazonaws.com");
    var awsBatchServiceRole = new Role("awsBatchServiceRoleRole", new()
    {
        Name = "awsBatchServiceRoleRole",
        AssumeRolePolicy = assumeRolePolicyBatch,
    });

    var awsBatchServiceRolePolicyAttachment = new RolePolicyAttachment("awsBatchServiceRoleRolePolicyAttachment", new()
    {
        Role = awsBatchServiceRole.Name,
        PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSBatchServiceRole",
    });
    
    var assumeRolePolicySpotfleet = AssumeInstancePolicy("spotfleet.amazonaws.com");
    var awsSpotfleetServiceRole = new Role("AmazonEC2SpotFleetTaggingRole", new()
    {
        Name = "AmazonEC2SpotFleetTaggingRole",
        AssumeRolePolicy = assumeRolePolicySpotfleet,
    });

    var sampleSecurityGroup = new SecurityGroup("batch-security-group", new()
    {
        Name = "batch-security-group",
        VpcId = defaultVpc.Id,
        Egress = new[]
        {
            new SecurityGroupEgressArgs
            {
                FromPort = 0,
                ToPort = 0,
                Protocol = "-1",
                CidrBlocks = new[]
                {
                    "0.0.0.0/0",
                },
            },
        },
    });

    
    
    var repoName = jobName;
    
    new Repository(repoName, new RepositoryArgs
    {
        Name = repoName
    });
    
    var defaultAza = GetDefaultSubnet('a');
    var defaultAzb = GetDefaultSubnet('b');
    var defaultAzc = GetDefaultSubnet('c');

    var computeEnvironment = new ComputeEnvironment("batch-demo-ec2", new()
    {
        ComputeEnvironmentName = "batch-demo-ec2",
        ComputeResources = new ComputeEnvironmentComputeResourcesArgs
        {
            InstanceRole = ecsInstanceRoleInstanceProfile.Arn,
            InstanceTypes = new InputList<string>
            {
                "optimal"
            },
            MaxVcpus = 32,
            MinVcpus = 0,
            SecurityGroupIds = new[]
            {
                sampleSecurityGroup.Id,
            },
            Subnets = new[]
            {
                defaultAza.Id,
                defaultAzb.Id,
                defaultAzc.Id,
            },
            Type = "EC2"
        },
        ServiceRole = awsBatchServiceRole.Arn,
        Type = "MANAGED",
    }, new CustomResourceOptions
    {
        DependsOn = new[]
        {
            awsBatchServiceRolePolicyAttachment
        },
    });
    
    new JobQueue(Constants.JobQueue, new()
    {
        Name = Constants.JobQueue,
        State = "ENABLED",
        Priority = 1,
        ComputeEnvironments = new[]
        {
            computeEnvironment.Arn
        },
    });

    var jobDefinitionRole = new Role(jobName, new RoleArgs
    {
        Name = jobName,
        AssumeRolePolicy = AssumeInstancePolicy("ecs-tasks.amazonaws.com")
    });
    
    new RolePolicyAttachment($"{jobName}-s3", new RolePolicyAttachmentArgs
    {
        Role = jobDefinitionRole.Name,
        PolicyArn = "arn:aws:iam::aws:policy/AmazonS3FullAccess"
    });

    new RolePolicyAttachment($"{jobName}-cloudwatch", new RolePolicyAttachmentArgs
    {
        Role = jobDefinitionRole.Name,
        PolicyArn = "arn:aws:iam::aws:policy/CloudWatchFullAccess"
    });
    
    new RolePolicyAttachment($"{jobName}-batch", new RolePolicyAttachmentArgs
    {
        Role = jobDefinitionRole.Name,
        PolicyArn = "arn:aws:iam::aws:policy/AWSBatchFullAccess"
    });
    
    var imageUrl = $"{caller.AccountId}.dkr.ecr.eu-west-1.amazonaws.com/{repoName}";
    var setupJobName = $"{jobName}-setup";
    new JobDefinition(setupJobName, new()
    {
        Name = setupJobName,
        Timeout = new JobDefinitionTimeoutArgs
        {
            AttemptDurationSeconds = (int)TimeSpan.FromHours(1).TotalSeconds
        },
        ContainerProperties = jobDefinitionRole.Arn.Apply(arn => 
            JsonConvert.SerializeObject(new 
            {
                command = new string[]{ },
                image = $"{imageUrl}:setup",
                jobRoleArn = arn,
                vcpus = 1,
                memory = 1024,
            }, Formatting.Indented)),
        Parameters = new InputMap<string>(),
        Type = "container",
    });
    var mapJobName = $"{jobName}-map";
    new JobDefinition(mapJobName, new()
    {
        Name = mapJobName,
        Timeout = new JobDefinitionTimeoutArgs
        {
            AttemptDurationSeconds = (int)TimeSpan.FromHours(1).TotalSeconds
        },
        ContainerProperties = jobDefinitionRole.Arn.Apply(arn => 
            JsonConvert.SerializeObject(new 
            {
                command = new string[]{  },
                image = $"{imageUrl}:map",
                jobRoleArn = arn,
                vcpus = 4,
                memory = 16_384,
            }, Formatting.Indented)),
        Parameters = new InputMap<string>(),
        Type = "container"
    });
    
    var reduceJobName = $"{jobName}-reduce";
    new JobDefinition(reduceJobName, new()
    {
        Name = reduceJobName,
        Timeout = new JobDefinitionTimeoutArgs
        {
            AttemptDurationSeconds = (int)TimeSpan.FromHours(1).TotalSeconds
        },
        ContainerProperties = jobDefinitionRole.Arn.Apply(arn => 
            JsonConvert.SerializeObject(new 
            {
                command = new string[]{  },
                image = $"{imageUrl}:reduce",
                jobRoleArn = arn,
                vcpus = 4,
                memory = 8192,
            }, Formatting.Indented)),
        Parameters = new InputMap<string>(),
        Type = "container"
    });

    // Export the name of the bucket
    return new Dictionary<string, object?>
    {
        ["bucketName"] = bucket.Id
    };
});
