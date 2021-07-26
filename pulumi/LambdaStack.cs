using Pulumi;
using Pulumi.Aws.ApiGatewayV2;
using Pulumi.Aws.ApiGatewayV2.Inputs;
using Pulumi.Aws.S3;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.Lambda.Inputs;

class LambdaStack : Stack
{
    public LambdaStack()
    {
        var tags = new InputMap<string>
        {
            { "User:Project",   Pulumi.Deployment.Instance.ProjectName },
            { "User:Stack",     Pulumi.Deployment.Instance.StackName }
        };
  
        // Create an AWS resource (S3 Bucket)
        var dataBucket = new Bucket("pulumi-demo", new BucketArgs()
        {
            Tags = tags
        });

        var lambda = new Function("quote-fetch-lambda", new FunctionArgs
        {
                Runtime = "dotnetcore3.1",
                Code = new FileArchive("../aws-lambda/publish"),
                Handler = "aws-lambda::Recumbent.Demo.Aws.AwsLambda::FunctionHandler",
                Environment = new FunctionEnvironmentArgs
                {
                    Variables = new InputMap<string>
                    {
                        { "QuoteServerHost", "https://0rogaeco5b.execute-api.eu-west-2.amazonaws.com/Prod" },
                        { "DataBucket", dataBucket.Id },
                    }              
                },
                Role = CreateLambdaRole(dataBucket).Arn,
                Timeout = 60,
                Tags = tags
        });
        


        var apiGateway = new Api("PulumiDemoGateway", new ApiArgs()
        {
            ProtocolType = "HTTP",
            Tags = tags
        });

        // Give API Gateway permissions to invoke the Lambda
        var lambdaPermission = new Permission("lambdaPermission", new PermissionArgs() 
        {
            Action = "lambda:InvokeFunction",
            Principal = "apigateway.amazonaws.com",
            Function = lambda.Name,
            SourceArn = Output.Format($"{apiGateway.ExecutionArn}/*/*"),
        },
        new CustomResourceOptions { DependsOn = { apiGateway, lambda } }
        );

        var integration = new Pulumi.Aws.ApiGatewayV2.Integration("lambdaIntegration", new IntegrationArgs()
        {
            ApiId = apiGateway.Id,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = lambda.Arn,
            IntegrationMethod = "POST",
            PayloadFormatVersion = "2.0",
            PassthroughBehavior = "WHEN_NO_MATCH",
        });

        var route = new Pulumi.Aws.ApiGatewayV2.Route("apiRoute", new RouteArgs()
        {
            ApiId = apiGateway.Id,
            RouteKey = "POST /", // "$default",
            Target = Output.Format($"integrations/{integration.Id}"),
        });

        var stage = new Pulumi.Aws.ApiGatewayV2.Stage("apiStage", new StageArgs()
        {
            ApiId = apiGateway.Id,
            Name = Pulumi.Deployment.Instance.StackName,
            RouteSettings = 
            {
                new StageRouteSettingArgs () 
                {
                    RouteKey = route.RouteKey,
                    ThrottlingBurstLimit = 5000,
                    ThrottlingRateLimit = 10000,
                }
            },
            AutoDeploy = true,
        },
        new CustomResourceOptions { DependsOn = { route } }
        );


        // Export the name of the bucket
        this.BucketName = dataBucket.Id;

        // Export the Lambda ARN
        this.Lambda = lambda.Arn;
        
        // Export the API endpoint
        this.ApiEndpoint = apiGateway.ApiEndpoint;
    }

    [Output]
    public Output<string> BucketName { get; set; }
    
    [Output]
    public Output<string> Lambda { get; set; }

    [Output]
    public Output<string> ApiEndpoint { get; set; }
    private static Role CreateLambdaRole(Bucket dataBucket)
    {
        var lambdaRole = new Role("lambdaRole", new RoleArgs
        {
            AssumeRolePolicy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {
                        ""Action"": ""sts:AssumeRole"",
                        ""Principal"": {
                            ""Service"": ""lambda.amazonaws.com""
                        },
                        ""Effect"": ""Allow"",
                        ""Sid"": """"
                    }
                ]
            }"
        });

        var logPolicy = new RolePolicy("lambdaLogPolicy", new RolePolicyArgs
        {
            Role = lambdaRole.Id,
            Policy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{
                    ""Effect"": ""Allow"",
                    ""Action"": [
                        ""logs:CreateLogGroup"",
                        ""logs:CreateLogStream"",
                        ""logs:PutLogEvents""
                    ],
                    ""Resource"": ""arn:aws:logs:*:*:*""
                }]
            }"
        });

        var bucketPolicy = new RolePolicy("lambdaBucketPolicy", new RolePolicyArgs
        {
            Role = lambdaRole.Id,
            Policy =
                Output.Format($@"{{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {{
                            ""Effect"": ""Allow"",
                            ""Action"": [
                                ""s3:*""
                            ],
                            ""Resource"": ""{dataBucket.Arn}""
                        }},
                        {{
                            ""Effect"": ""Allow"",
                            ""Action"": [
                                ""s3:*""
                            ],
                            ""Resource"": ""{dataBucket.Arn}/*""
                        }}
                    ]
                }}")
        });

        return lambdaRole;
    }
}