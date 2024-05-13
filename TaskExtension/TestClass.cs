using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskExtension;
internal class TestClass
{
    public async Task Test()
    {
        var task = Task.Delay(1000);

        await task.Catch<Exception>(x => { });

        var result = await GetStringAsync().Catch<NullReferenceException>(x => null);

        var intresult = await Task.FromResult(1).Catch<NullReferenceException>(x => 1);
    }
    public Task<IEnumerable<string>> GetStringAsync()
        => Task.FromResult(Enumerable.Empty<string>());
}
