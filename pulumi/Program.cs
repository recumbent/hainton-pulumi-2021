using System;
using System.Threading.Tasks;
using Pulumi;

class Program
{
    static Task<int> Main()
    {
        var stack = Environment.GetEnvironmentVariable("PULUMI_STACK");
        Console.WriteLine($"*** Deploying stack: {stack}");
    
        return stack switch
        {
            string s when s.StartsWith("azure") => Deployment.RunAsync<FunctionStack>(),
            string s when s.StartsWith("aws") => Deployment.RunAsync<LambdaStack>(),
            _ => throw new ArgumentOutOfRangeException("stack", "Known stacks start 'azure' or 'aws'.")
        };
    }
    
}
