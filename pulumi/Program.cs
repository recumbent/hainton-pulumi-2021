using System;
using System.Threading.Tasks;
using Pulumi;

class Program
{
    static Task<int> Main() 
    {
        var stack = Environment.GetEnvironmentVariable("PULUMI_STACK");
        Console.WriteLine($"Running for stack: {stack}");
        
        return Deployment.RunAsync<FunctionStack>();
    }
}
