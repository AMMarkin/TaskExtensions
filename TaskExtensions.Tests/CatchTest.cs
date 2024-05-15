namespace TaskExtensions.Tests;

public class CatchTest
{
    [Theory]
    [MemberData(nameof(GetTestData))]
    public async Task NonTypedTasksTest(Task task, object? expectedResult, Type? expectedException)
    {
        var isFaulted = expectedException is not null;
        var catched = false;
        Type? catchedException = null;
        object? result = null;
        int activatedHandler = 0;

        var expectedHandler = expectedException?.Name switch
        {
            null => 0,
            nameof(NullReferenceException) => 1,
            nameof(FileNotFoundException) => 2,
            _ => 3
        };

        await task.Catch<NullReferenceException>(e => HandleException(e, 1))
                  .Catch<FileNotFoundException>(e => HandleException(e,2))
                  .Catch(e => HandleException(e,3));

        if (expectedResult is not null)
        {
            result = task.GetType().GetProperty("Result")!.GetValue(task);
        }

        Assert.Equal(isFaulted, catched);
        Assert.Equal(expectedException, catchedException);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedHandler, activatedHandler);

        void HandleException(Exception e, int handlerIndex)
        {
            catched = true;
            catchedException = e.GetType();
            activatedHandler = handlerIndex;
        }
    }

    public static IEnumerable<object?[]> GetTestData()
    {
        foreach (var (task, expectedResult, expectedException) in GetValues())
            yield return [task, expectedResult, expectedException];

        static IEnumerable<(Task task, object? expectedResult, Type? expectedException)> GetValues()
        {
            yield return (FaultedTask<NullReferenceException>(), null, typeof(NullReferenceException));
            yield return (Task.FromResult(1), 1, null);
            yield return (Task.FromResult("string"), "string", null);
            yield return (FaultedTask<int, NullReferenceException>(), null, typeof(NullReferenceException));
        }
    }

    private static Task FaultedTask<TException>() where TException : Exception, new() => Task.FromException(new TException());
    private static Task<TResult> FaultedTask<TResult, TException>() where TException : Exception, new() => Task.FromException<TResult>(new TException());
}

