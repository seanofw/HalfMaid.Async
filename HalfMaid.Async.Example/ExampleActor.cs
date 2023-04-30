using System;
using System.Threading.Tasks;

namespace HalfMaid.Async.Example
{
	public class ExampleActor : AsyncGameObjectBase
	{
		public int Id { get; }

		private static int _actorIdSource = 0;

		public ExampleActor()
		{
			Id = ++_actorIdSource;
		}

		public async GameTask Main()
		{
			Console.WriteLine($"Actor {Id}: Run start");
			int result1 = await Shallow();
			Console.WriteLine($"Actor {Id}: Run middle");
			int result2 = await Shallow();
			Console.WriteLine($"Actor {Id}: Run end: {result1 + result2} == 6");
		}

		private async GameTask<int> Shallow()
		{
			int amount = 1;
			Console.WriteLine($"Actor {Id}: Shallow start");
			await Deep();
			amount++;
			await RunTask(async () =>
			{
				Console.WriteLine($"Actor {Id}: Before Task.Delay");
				await Task.Delay(1000);
				Console.WriteLine($"Actor {Id}: After Task.Delay");
			});
			Console.WriteLine($"Actor {Id}: Shallow middle");
			await Deep();
			amount++;
			Console.WriteLine($"Actor {Id}: Shallow end");
			return amount;
		}

		private async GameTask Deep()
		{
			Console.WriteLine($"Actor {Id}: Deep start");
			await Next();
			Console.WriteLine($"Actor {Id}: Deep middle");
			await Delay(2);
			Console.WriteLine($"Actor {Id}: Deep end");
		}
	}
}
