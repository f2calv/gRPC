using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
namespace CasCap.Service
{
    public class GreeterService : Greeter.GreeterBase
    {
        readonly ILogger<GreeterService> _logger;

        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloResponse> SayHello(HelloRequest request, ServerCallContext context)
        {
            var c1 = context;
            var ctx = context.GetHttpContext();

            _logger.LogInformation(nameof(SayHello));
            return Task.FromResult(new HelloResponse
            {
                Message = "Hello " + request.Name
            });
        }

        public override Task LotsOfReplies(HelloRequest request, IServerStreamWriter<HelloResponse> responseStream, ServerCallContext context)
        {
            _logger.LogInformation(nameof(LotsOfReplies));
            return Task.FromResult(new HelloResponse
            {
                Message = "ok"
            });
        }
    }
}