using System;

namespace HalfMaid.Async.Example
{
	public static class Program
	{
		public static void Main()
		{
			Console.WriteLine("Start");

			GameTaskRunner runner = AsyncGameObjectBase.Runner = new GameTaskRunner();

			ExampleActor actor = new ExampleActor();

			runner.StartImmediately(actor.Main);

			while (runner.TaskCount != 0)
			{
				Console.WriteLine("Continuing");
				runner.RunNextFrame();
				Console.WriteLine("Continued");
			}

			Console.WriteLine("End");
		}
	}
}
