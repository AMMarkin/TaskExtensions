namespace TaskExtension;
internal class TestClass
{
    public async Task Test()
    {
        var task = Task.Delay(1000);

        await task.Catch<NullReferenceException>(x => { });

        var result = await GetStringAsync().Catch<NullReferenceException>(x => null!)
            .Catch(e => throw e);

        var intResult = await Task.FromResult(1).Catch<NullReferenceException>(x => 1);

        var tResult = await Test<int>().Catch<NullReferenceException>(x => 1);

        var withAnonimousTypes = Enumerable.Range(1, 100)
            .Select(x => new { value = x, text = x.ToString() })
            .Select(x => x.value);
    }

    public Task<IEnumerable<string>> GetStringAsync() => null!;

    public Task<TResult> Test<TResult>() =>null!;

    public Task<Result<TResult>> Test2<TResult>() => null!;

    public class Result<T>;
}
