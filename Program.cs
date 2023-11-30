using Microsoft.Extensions.Hosting;

// namespace Octopus_Energy_iOS_Shortcut_Serverless_Backend;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
