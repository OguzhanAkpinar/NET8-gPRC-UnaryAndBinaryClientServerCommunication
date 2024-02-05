using Google.Protobuf;
using Grpc.Net.Client;
using MyWorksgRPCClient;
using static MyWorksgRPCClient.Sample;

const string channelUrl = "http://localhost:5253";

//await RunSimpleData();
//await RunClientStream();
//await RunServerStream();
//await RunTwoWayStream();
//await RunFileUpload();
await RunFileDownload();

Console.ReadLine();

async Task RunSimpleData()
{
    GrpcChannel channel = GrpcChannel.ForAddress(channelUrl);
    SampleClient sampleClient = new Sample.SampleClient(channel);
    var response = sampleClient.GetSimpleData(new SimpleRequest { Message = "Sample data" });
    Console.WriteLine($"Received message : {response.Message}");
}

async Task RunClientStream()
{
    GrpcChannel channel = GrpcChannel.ForAddress(channelUrl);
    SampleClient sampleClient = new Sample.SampleClient(channel);
    var request = sampleClient.GetClientStream();

    await Task.Run(async () =>
    {
        int count = 0;
        while (++count <= 10)
        {
            await request.RequestStream.WriteAsync(new StreamRequest { Message = $"Sent message {count}" });
            await Task.Delay(1000);
        }
    });

    await request.RequestStream.CompleteAsync();

    var response = await request;
    Console.WriteLine($"Received message : {response.Message}");
}

async Task RunServerStream()
{
    GrpcChannel channel = GrpcChannel.ForAddress(channelUrl);
    SampleClient sampleClient = new Sample.SampleClient(channel);
    var messageResponse = sampleClient.GeServerStream(new StreamRequest { Message = Console.ReadLine() });

    await Task.Run(async () =>
    {
        while (await messageResponse.ResponseStream.MoveNext(new System.Threading.CancellationToken()))
            Console.WriteLine($"Received message : {messageResponse.ResponseStream.Current.Message}");
    });
}

async Task RunTwoWayStream()
{
    GrpcChannel channel = GrpcChannel.ForAddress(channelUrl);
    SampleClient sampleClient = new Sample.SampleClient(channel);
    var streamMessage = sampleClient.GetTwoWayStream();

    Task request = Task.Run(async () =>
    {
        int count = 0;
        while (++count <= 10)
        {
            await streamMessage.RequestStream.WriteAsync(new StreamRequest { Message = $"Sent message {count}" });
            await Task.Delay(1000);
        }
    });

    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    Task response = Task.Run(async () =>
    {
        while (await streamMessage.ResponseStream.MoveNext(cancellationTokenSource.Token))
            Console.WriteLine(streamMessage.ResponseStream.Current.Message);
    });
    await request;
    await streamMessage.RequestStream.CompleteAsync();
    await response;
}

async Task RunFileDownload()
{
    var channel = GrpcChannel.ForAddress(channelUrl);
    var client = new Sample.SampleClient(channel);

    string downloadPath = @"C:\Users\OguzhanAkpinar\Downloads\MyWorksgRPCClient\DownloadFile";

    var fileInfo = new MyWorksgRPCClient.FileInfo
    {
        FileExtension = ".pdf",
        FileName = "SampleDocument"
    };

    FileStream fileStream = null;

    var request = client.FileDownLoad(fileInfo);

    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    int count = 0;
    decimal chunkSize = 0;

    while (await request.ResponseStream.MoveNext(cancellationTokenSource.Token))
    {
        if (count++ == 0)
        {
            fileStream = new FileStream(@$"{downloadPath}\{request.ResponseStream.Current.Info.FileName}{request.ResponseStream.Current.Info.FileExtension}", FileMode.CreateNew);

            fileStream.SetLength(request.ResponseStream.Current.FileSize);
        }

        var buffer = request.ResponseStream.Current.Buffer.ToByteArray();

        await fileStream.WriteAsync(buffer, 0, request.ResponseStream.Current.ReadByte);

        Console.WriteLine($"{Math.Round(((chunkSize += request.ResponseStream.Current.ReadByte) * 100) / request.ResponseStream.Current.FileSize)}%");
    }
    Console.WriteLine("Downloaded...");

    await fileStream.DisposeAsync();
    fileStream.Close();
}

async Task RunFileUpload()
{
    var channel = GrpcChannel.ForAddress(channelUrl);
    var client = new Sample.SampleClient(channel);
    string file = @"C:\Users\OguzhanAkpinar\Downloads\MyWorksgRPCClient\DownloadFile\SampleDocument.pdf";

    using FileStream fileStream = new FileStream(file, FileMode.Open);

    var content = new BytesContent
    {
        FileSize = fileStream.Length,
        ReadByte = 0,
        Info = new MyWorksgRPCClient.FileInfo { FileName = "SampleDocument", FileExtension = ".pdf" }
    };

    var upload = client.FileUpLoad();

    byte[] buffer = new byte[2048];

    while ((content.ReadByte = fileStream.Read(buffer, 0, buffer.Length)) > 0)
    {
        content.Buffer = ByteString.CopyFrom(buffer);
        await upload.RequestStream.WriteAsync(content);
    }
    await upload.RequestStream.CompleteAsync();

    fileStream.Close();
}