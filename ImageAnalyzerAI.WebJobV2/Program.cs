using ImageAnalyzerAI.WebJobV2;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<QueueWorker>();

var host = builder.Build();
host.Run();
