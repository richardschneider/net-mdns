# Cancelling a request

All requests to the IPFS engine can be cancelled by supplying 
an optional [CancellationToken](xref:System.Threading.CancellationToken).  When 
the token is cancelled, 
a [TaskCanceledException](xref:System.Threading.Tasks.TaskCanceledException) 
will be `thrown`.

Here's a contrived example ([unit test](https://github.com/richardschneider/net-ipfs-api/blob/cancellation/test/CoreApi/CancellationTest.cs)) 
that forces the getting of info on the local IPFS server to be cancelled

```cs
var cts = new CancellationTokenSource(500);
try
{
	await Task.Delay(1000);
	var peer = await ipfs.Generic.IdAsync(cts.Token);
	Assert.Fail("Did not throw TaskCanceledException");
}
catch (TaskCanceledException)
{
	return;
}
```

See also [Task Cancellation](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)
