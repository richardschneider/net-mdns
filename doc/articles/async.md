# Asynchronous I/O

All requests to the IPFS engine are [asynchronous](https://docs.microsoft.com/en-us/dotnet/csharp/async),
which does not block current thread.

This means that callers should **normally** use the `async/await` paradigm

```cs
async Task<Cid> AddText()
{
	var data = await ipfs.FileSystem.AddTextAsync("I am pinned");
	return data.Id;
}
```

If a synchronous operation is required, then this can work

```cs
Cid AddText()
{
	var data = ipfs.FileSystem.AddTextAsync("I am pinned").Result;
	return data.Id;
}
```

Or use `.Wait()` instead of `.Result` when the operation returns nothing.
