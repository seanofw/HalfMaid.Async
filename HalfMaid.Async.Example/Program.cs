using System;
using System.Threading;
using System.Threading.Tasks;

namespace HalfMaid.Async.Example
{
	public static class Program
	{
		public static void Main()
		{
			Console.WriteLine("Start");

			GameTaskRunner runner = AsyncGameObjectBase.Runner = new GameTaskRunner();

			ExampleActor actor = new ExampleActor();
			//Task.Run(() => actor.Main()).Wait();

			runner.StartImmediately(actor.Main);

			while (runner.TaskCount != 0)
			{
				Thread.Sleep(100);
				Console.WriteLine($"Frame {runner.Frame}");
				runner.RunNextFrame();

				if (runner.Frame == 10)
				{
					ExampleActor actor2 = new ExampleActor();
					runner.StartImmediately(actor2.Main);
				}

				//if (runner.Frame == 20)
				//{
				//	runner.CancelAllTasks();
				//}
			}

			Console.WriteLine("End");
		}
	}
}
