using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace MyWorks.gRPC.Services
{
    public class SampleService : Sample.SampleBase
    {
        private readonly ILogger<SampleService> _logger;
        readonly IWebHostEnvironment _webHostEnvironment;

        public SampleService(ILogger<SampleService> logger, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public override Task<SimpleReply> GetSimpleData(SimpleRequest request, ServerCallContext context)
        {
            return Task.FromResult(new SimpleReply
            {
                Message = "Response message: " + request.Message
            });
        }

        public override async Task GeServerStream(StreamRequest request, IServerStreamWriter<StreamReply> responseStream, ServerCallContext context)
        {
            await Task.Run(async () =>
            {
                int count = 0;
                while (++count <= 10)
                {
                    await responseStream.WriteAsync(new StreamReply { Message = $"Request message {count}" });
                    await Task.Delay(1000);
                }
            });
        }

        public override async Task<StreamReply> GetClientStream(IAsyncStreamReader<StreamRequest> requestStream, ServerCallContext context)
        {
            await Task.Run(async () =>
            {
                while (await requestStream.MoveNext())
                {
                    Console.WriteLine($"Message received.");
                    Console.WriteLine("Request message : ");
                    Console.WriteLine(requestStream.Current.Message);
                }
            });

            return new StreamReply { Message = "Request received and processed..." };
        }

        public override async Task GetTwoWayStream(IAsyncStreamReader<StreamRequest> requestStream, IServerStreamWriter<StreamReply> responseStream, ServerCallContext context)
        {
            Task response = Task.Run(async () =>
            {
                int count = 0;
                while (++count <= 10)
                {
                    await responseStream.WriteAsync(new StreamReply { Message = $"{count}. Request received and processed..." });
                    await Task.Delay(1000);
                }
            });

            Task request = Task.Run(async () =>
            {
                while (await requestStream.MoveNext())
                {
                    Console.WriteLine($"Message received.");
                    Console.WriteLine("Request message : ");
                    Console.WriteLine(requestStream.Current.Message);
                }
            });

            await response;
            await request;
        }

        public override async Task FileDownLoad(FileInfo request, IServerStreamWriter<BytesContent> responseStream, ServerCallContext context)
        {
            string path = Path.Combine(_webHostEnvironment.ContentRootPath, "files");
            using FileStream fileStream = new FileStream($"{path}/{request.FileName}{request.FileExtension}", FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[2048];

            BytesContent content = new BytesContent
            {
                FileSize = fileStream.Length,
                Info = new FileInfo { FileName = "SampleDocument", FileExtension = ".pdf" },
                ReadByte = 0
            };

            while ((content.ReadByte = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                content.Buffer = ByteString.CopyFrom(buffer);
                await responseStream.WriteAsync(content);
            }

            fileStream.Close();
        }

        public override async Task<Empty> FileUpLoad(IAsyncStreamReader<BytesContent> requestStream, ServerCallContext context)
        {
            string path = Path.Combine(_webHostEnvironment.ContentRootPath, "files");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            FileStream fileStream = null;

            int count = 0;

            decimal chunkSize = 0;

            while (await requestStream.MoveNext())
            {
                if (count++ == 0)
                {
                    fileStream = new FileStream($"{path}/{requestStream.Current.Info.FileName}{requestStream.Current.Info.FileExtension}", FileMode.CreateNew);
                    fileStream.SetLength(requestStream.Current.FileSize);
                }
                var buffer = requestStream.Current.Buffer.ToByteArray();
                await fileStream.WriteAsync(buffer, 0, requestStream.Current.ReadByte);
                Console.WriteLine($"{Math.Round(((chunkSize += requestStream.Current.ReadByte) * 100) / requestStream.Current.FileSize)}%");
            }
            Console.WriteLine("Uploaded...");
            await fileStream.DisposeAsync();
            fileStream.Close();
            return new Empty();
        }
    }
}