var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

builder.AddProject<Projects.Redis_Invalidator>("redis-invalidator").WithReference(cache);

builder.AddProject<Projects.Redis_Loader>("redis-loader").WithReference(cache);

builder.Build().Run();
