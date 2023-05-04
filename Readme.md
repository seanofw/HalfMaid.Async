# HalfMaid.Async

Copyright &copy; 2023 by Sean Werkema

Licensed under the [MIT open-source license](https://opensource.org/license/mit/)

## Overview

This repository contains the HalfMaidGames Async library, which is designed to solve a common problem in video-game programming in C#:  The difficulty of building video-game state machines.

Instead of using `switch`-statements or hacking `IEnumerable` generators for your state machines, you can use nice, clean `async`/`await`-based programming for each actor in your game, and it scales very well to complex use cases.

## Installation

You can install the latest [HalfMaidGames.Async library]() as a Nuget package.

The package is multi-targeted for .NET Core 2.1, .NET Core 3.1, .NET 5.0, and .NET 6.0+ to provide maximum backward compatibility.  .NET Framework 4.x and earlier and .NET Core 1.x are not supported.

## Example &amp; Rationale

### The Problem

Consider an enemy character that moves back and forth every ten seconds.  In a state-machine-based model, you might write code that's something like this:

```cs
public class BackAndForthEnemy
{
	public float X;
	public float Y;

	public int State;
	public int Counter;

	public void Update()
	{
		switch (State) {
			case 0:
				// Initial state.
				State = 1;
				Counter = 600;
				break;

			case 1:
				// Move right for 10 seconds.
				X += 0.1f;
				if (--Counter == 0)
				{
					State = 2;
					Counter = 600;
				}
				break;

			case 2:
				// Move left for 10 seconds.
				X -= 0.1f;
				if (--Counter == 0)
				{
					State = 1;
					Counter = 600;
				}
				break;
		}
	}
}
```
Every 1/60th of a second, we perform a little bit of the action — but it's completely tangled.  The logic and control flow is inside-out, because you need to return to the main game loop after every update.

### The Ideal

What you really *want* to be able to write is simple, procedural code like this:
```cs
public class BackAndForthEnemy
{
	public float X;
	public float Y;

	public void Main()
	{
		// Loop forever.
		while (true)
		{
			// Move right for 10 seconds.
			Move(+0.1f, 10);

			// Move left for 10 seconds.
			Move(+0.1f, 10);
		}
	}

	private void Move(float amountPerFrame, float seconds)
	{
		for (float i = 0; i < seconds * 60; i++)
		{
			X += amountPerFrame;
			wait_for_next_frame;
		}
	}
}
```

### The Solution

With the HalfMaidGames Async library, you can use C#'s `async`/`await` to write code that looks almost exactly like the procedural example above:
```cs
public class BackAndForthEnemy : AsyncGameObjectBase
{
	public float X;
	public float Y;

	public async GameTask Main()
	{
		// Loop forever.
		while (true)
		{
			// Move right for 10 seconds.
			await Move(+0.1f, 10);

			// Move left for 10 seconds.
			await Move(+0.1f, 10);
		}
	}

	private async GameTask Move(float amountPerFrame, float seconds)
	{
		for (float i = 0; i < seconds * 60; i++)
		{
			X += amountPerFrame;
			await Next();
		}
	}
}
```
The Async library "magically" interrupts the methods at each call to `Next()`, and it resumes at that exact point in the next frame, so that the code for your actors can be written as though each one is just logical, procedural code.

### Running it

How do you use your new `async`-based enemy?  The Async library contains a `GameTaskRunner` that is responsible for _running_ these "game tasks."  Each frame, the runner makes each "game task" run forward until the task calls `Next()` or exits.

Using the runner is extremely simple:
```cs
public void ExampleProgram()
{
	// Create a new runner, and use it as the default runner for everything
	// that inherits from AsyncGameObjectBase.
	GameTaskRunner runner = new GameTaskRunner();
	AsyncGameObjectBase.Runner = runner;

	// Create our enemy and start running it.
	BackAndForthEnemy enemy = new BackAndForthEnemy();
	runner.StartImmediately(enemy.Main);

	while (true)
	{
		// Run whatever GameTasks are in progress for one frame.
		runner.RunNextFrame();

		// Render the next frame (pseudocode: however you display frames).
		graphics.Clear();
		enemy.Render(graphics);
		graphics.SwapBuffers();
	}
}
```
In short, the only methods you really need to know on the runner are `StartImmediately()`, which starts running an `async` method, and `RunNextFrame()`, which runs anything that needs to run in the next frame.

## Usage

### GameTasks

Any method that returns a `GameTask` can be run by the runner.  However, typically you will want to use `Task`-like patterns:

1.  Declare your `GameTask` methods `async`.
2.  Use `await` inside them when invoking other `GameTask` methods.
3.  You can `await Next()` or `await Delay()` as deeply in the call chain as you want, as long as all callers `await` your method as well.
4.  If your method needs to return a value, return `GameTask<T>`.

#### Detailed Samples

Declaring methods that either use `Next()` or `Delay()`, or that call methods that use `Next()` or `Delay()`:
```cs
public async GameTask MyMethod()
{
	...
	await runner.Next();
	...
}
```
Declaring `GameTask` methods that return data:
```cs
int amount = await MyMethod();

...

public async GameTask<int> MyMethod()
{
	...
	await runner.Next();
	...
	return 5;
}
```
Waiting until the next frame to perform actions slowly:
```cs
...do something...

await runner.Next();

...do something...

await runner.Next();

...do something...
```
Waiting for many frames to perform actions even slower:
```cs
...do something...

await runner.Delay(10);

...do something...
```
(The parameter to the `Delay()` method is the number of *frames* to wait, not milliseconds or seconds.  And the duration of a frame depends solely on how many frames you choose to execute per second — on how often you call `runner.RunNextFrame()`.)

#### Complete Example

Here's a simple example showing all of these pieces together to build an enemy that "thinks" for a few seconds and then moves in a random direction for a few seconds.  If this were built as a traditional state machine, the code would be much more complex, and much harder to read and to modify, but as `async`-style code, it's simple and straightforward:

```cs
public class RandomEnemy
{
	private const int FramesPerSecond = 60;

	private Random _random = new Random();

	public Vector2 Position;
	public bool IsDead;

	public async GameTask Main()
	{
		while (!IsDead)
		{
			Direction d = await ChooseRandomDirection();
			await Move(d, 3.0f /*seconds*/);
		}
	}

	private async GameTask<Direction> ChooseRandomDirection()
	{
		await Delay(FramesPerSecond * 3 /*seconds*/);
		return (Direction)(_random.Next() % 4);
	}

	private async Move(Direction d, float time)
	{
		Vector2 movement = d.ToUnitVector() * 0.1;
		for (float i = 0; i < time * FramesPerSecond; i++)
		{
			Position += movement;
			await Next();
		}
	}
}
```

### Starting GameTasks

"Registering" your new actor to run inside a `GameTaskRunner` involves little more than starting the outermost method of your code:
```cs
GameTaskRunner runner = new GameTaskRunner();
RandomEnemy enemy = new RandomEnemy();
runner.StartImmediately(enemy.Main);
```

You can "register" on-the-fly lambda code as well:  Any method that is declared `async GameTask` can be managed by a runner:
```cs
GameTaskRunner runner = new GameTaskRunner();
RandomEnemy enemy = new RandomEnemy();

runner.StartImmediately(async () => {
	...do something...

	await runner.Delay(10);

	...do something more...
});
```

### Main Loop

You may be wondering what you need to change in your main loop to support this, or you may be concerned about your ability to integrate this in your existing engine.  Don't worry!  This is designed to be very easy to integrate:

1.  You must create at least one `GameTaskRunner` instance.
2.  You must call `runner.StartImmediately()` or `runner.StartYielded()` to start any `async GameTask` methods.
3.  You must call `runner.RunNextFrame()` at least once per frame.
4.  You *should* call `runner.RunUntilAllTasksFinish()` before your game exits, if there's any chance any tasks are still running and you want them to finish.

These requirements should be compatible with most game engines and game frameworks, even those you write yourself.

Importantly, unlike `Task.Run()`, all `GameTask`s managed by the `GameTaskRunner` are run *on the calling thread*.  When you call `runner.RunNextFrame()`, each `GameTask` runs synchronously until it invokes `await Next()` or `await Delay()`, and then the next `GameTask` runs synchronously after it, until all have had a chance to execute.  `async`/`await` here does not mean running in another thread, but rather *cooperative multitasking* in a single thread: the same threading behavior that you would produce using simpler `switch`-based state machines.

You can create more than one `GameTaskRunner` instance, if you want to support localized tasks in a part of your code base.  The runners are independent of each other, and unlike many task-async libraries, there are no static fields or properties on any of the `GameTask`-related classes.

But in other words, your integration can be this simple:
```cs
while (true)
{
	runner.RunNextFrame();

	...update...
	...render...
	...wait for next frame...
}
```

Or if you're using a framework that exposes an `OnUpdate`-like event handler that executes each frame, you can simply put `RunNextFrame()` inside `OnUpdate`:
```cs
public GameTaskRunner Runner { get; }

public MyClass()
{
	Runner = new GameTaskRunner();
}

protected override void OnUpdate()
{
	base.OnUpdate();

	Runner.RunNextFrame();

	...do any other updates you need here..
}
```
Some form of these patterns should fit nearly every game written in C#.

### AsyncGameObjectBase

You are *not* required to use `AsyncGameObjectBase`.  It's a simple, small *optional* base class that makes accessing the runner's methods easy by anything that inherits it:

```cs
public abstract class AsyncGameObjectBase
{
	public static GameTaskRunner Runner { get; set; }

	public GameTaskYieldAwaitable Next() => Runner.Next();
	public GameTaskYieldAwaitable Delay(int frames) => Runner.Delay(frames);
	public ExternalTaskAwaitable RunTask(Func<Task> task) => Runner.RunTask(task);
}
```

### External Tasks

Sometimes you may want to use a "normal" `Task` object within the scope of a `GameTask`.  For example, you may need to perform slow file I/O, or network I/O, and you would still like your game loop to run.  The `GameTaskRunner` provides a special method, `RunTask()`, that allows `Task` objects to be integrated within a `GameTask`'s execution.

Recall above that `GameTask`s are run synchronously until each reaches `await Next()` or `await Delay()`:  This behavior is very different from the normal usage of `await`.  Therefore, `RunTask()` is needed to "connect" `Task` objects, which have an inherent notion of threading and asynchronicity, to `GameTask`s, which are state machines in disguise.

It is not hard to embed a `Task` inside a `GameTask`:  Simply `await RunTask(task)` inside your `GameTask` method, as in the example below, similarly to how you might call `Task.Run(task)`:
```cs
public async GameTask DoSomething()
{
	...
	await Next();
	...
	await RunTask(async () => {
		...
		await Task.Delay(1000);		// Do Task-based slow operations
		...
	});
	...
	await Next();
	...
}
```
Inside the body of the task passed to `RunTask()`, you may use any normal `Task` object.  When that `Task` completes, the outer `GameTask` will then continue on the next available frame of execution.  Each real `Task` will be executed on its own thread using the standard .NET thread pool.  While a `Task` is executing, the `GameTask` around it will be put to sleep and will not block the runner.

### Bulk Cancellation

The `GameTaskRunner` includes special logic for cancelling _all_ active GameTasks at the same time.  For example, you may need to do this when your game switches states (start screen --> main gameplay) and needs to use a completely different set of GameTasks in the new state.  Or you may need it when your game exits or when it saves to disk, to be able to stop all GameTasks at once.

There are is a special API on `GameTaskRunner` for these needs:

- `CancelAllTasks(createException, handleUncaughtExceptions)` - Raise an exception inside every active GameTask.  Both parameters are optional.

By default, a `TaskCanceledException` will be raised.  You can instead pass a `Func<Exception>` to `CancelAllTasks()` as its first parameter; this method must construct an instance of an exception to be raised.  It will be invoked once per GameTask.

Cancellation exceptions are normally discarded by `CancelAllTasks()` if they rise fully outside the task.  You can provide alternative handling by passing an `Action<Action>` to `CancelAllTasks()` as its second parameter.  Your `handleUncaughtExceptions` function should invoke the action given to it, wrapping it in appropriate exception handling.  For example:

```cs
runner.CancelAllTasks(() => new FooException(), MyExceptionHandler);

...

private void MyExceptionHandler(Action action)
{
	try
	{
		// Continue running the task.  A FooException() will be
		// raised wherever it last paused.
		action();
	}
	catch (FooException)
	{
		// Do something special here.
	}
}
```

If external `Task`s are active that were started by `runner.RunTask()`, `CancelAllTasks()` will wait for them to complete before cancelling the `GameTask`s that invoked them:  It cannot automatically cancel external `Task`s.  If you intend to cancel a `GameTask` that calls `runner.RunTask()`, you should provide a means to cancel that external task yourself, such as by triggering a `CancellationToken` before calling `runner.CancelAllTasks()`.

Note that while there is support for `CancelAllTasks()`, there is presently no way to cancel a _single_ task:  It's all-or-nothing.

## APIs

There are relatively few public APIs, as the library mostly relies on standard `async`/`await` mechanics to function.  But here are the ones that are exposed:

### GameTask

This is a `class` that represents an active task for a function that otherwise would return `void`.  It may be executed via normal `await`/`async`.

This is combined with its own builder type to keep heap overhead as low as possible.  It consists of about 5 or 6 pointers' worth of data.

`GameTask` may be safely copied and moved around, since it is only a reference to a class and some additional methods.

- **Property `GameTaskStatus Status`**:  The current status of this task, either `InProgress`, `Success` (completed without an exception), or `Failed` (threw an exception).
- **Method `Then(Func<GameTask> task)`**:  This can be used to schedule a `GameTask` to run after this one completes, somewhat like `Task.ContinueWith()` does.  Note that the prior task's status (success or failure) is *not* provided to the next task.
- **Property `IsCompleted`**: True if this task has ended (via normal completion or an exception), false if it is still `InProgress`.
- **Method `GetAwaiter()`**: Returns an awaiter-compatible object that can be used by `await` to trigger any continued computation in this task.  You generally do not need to call this.

This class also has certain public methods that are required to implement the `AsyncMethodBuilder` pattern.  Even though they are declared `public`, they should only be invoked by the C# compiler itself.

### GameTask<T>

This is a similar `class` to `GameTask`, and most of the above description applies.  This inherits from `GameTask`.  It also has the following property:

- **Property `T Result`**: The result (return value) of this task after it has successfully completed.  Will be `default(T)` until the task successfully completes.

### GameTaskRunner

This manages the active state of a group of tasks, and can run those tasks forward to a specific point in time, either one frame, several frames, or all frames.

This class is thread-safe:  Any method or property below may be invoked from any thread.  Generally, only one thread should be the caller of `RunNextFrame()` or `RunUntilAllTasksFinish()`, though.

- **Property `TaskCount`** - This returns a count of how many `InProgress` `GameTask`s are being tracked by the runner.  When this count reaches zero, all `GameTask`s have either completed successfully or thrown exceptions, and none have any remaining work.
- **Method `EnqueueFuture(Action action, int frames)`** - Enqueue an action to occur at some point in the future (the current time plus the given number of frames).  This overload takes a simple method as the action to perform.
- **Method `EnqueueFuture(Func<GameTask> action, int frames)`** - Enqueue an action to occur at some point in the future (the current time plus the given number of frames).  This overload takes a `Func<GameTask>`, i.e., an `async GameTask` method, which will be started the given number of frames in the future.
- **Method `Next()`** - This returns an awaitable that resolves during the next frame of execution.  It should always be called as `await Next()`.  It is conceptually similar to `await Task.Yield()`.
- **Method `Delay(int frames)`** - This returns an awaitable that resolves in a future frame of execution.  It should always be called as `await Delay(frames)`.  It is conceptually similar to `await Task.Delay(msec)`.
- **Method `StartImmediately(Func<GameTask> action)`** - This causes the given action to be started (run/called/invoked) immediately; if it encounters an `await` during its execution that would cause it to block, its continuation will be registered with the runner, and then this call will return.  This is conceptually similar to `Task.Run(action)`.
- **Method `StartYielded(Func<GameTask> action)`** - This causes the given action to be started (run/called/invoked) during the next frame, and returns immediately.  This is conceptually similar to a pattern like `Task.Run(async () => { await Task.Yield(); await action(); })`.
- **Method `RunUntilAllTasksFinish()`** - This executes all remaining registered tasks in a tight loop until all `GameTask`s and external `Task`s have either finished successfully or thrown exceptions, and then it returns.  This should be used at the end of your program (or of the `GameTaskRunner`'s lifetime) to ensure that any `finally` or `using` statements within any active tasks are eventually properly completed.
- **Method `RunNextFrame()`** - Run exactly one subsequent frame's worth of execution for any registered tasks.  As soon as all tasks have either completed or have invoked `Next()` or `Delay()` to wait for a subsequent frame, this method returns.
- **Method `RunTask(Func<Task> task)`** - Allow a traditional I/O task to be executed and managed by the task runner.  The `Task` will be executed by the thread pool.
- **Method `CancelAllTasks<TException>(Func<TException> createException, Action<Action>? handleUncaughtExceptions)`** - Cancel all active GameTasks by raising exceptions inside them.  You can provide an optional custom function to create the exceptions.  You can provide an optional custom handler for any uncaught exceptions.  If a creation function is not provided, this will create `TaskCanceledException`s on its own.

### AsyncGameObjectBase

This is a convenience class.  You do not need to inherit from it, but doing so can simplify calling methods like `GameTaskRunner.Next()` in your own code.

- **Static property `GameTaskRunner Runner`** - The runner that will be used by this object.  This is initialized by default to `new GameTaskRunner()`.
- **Method `Next()`** - A simple proxy to `Runner.Next()`, this allows child classes to simply write `await Next()`.
- **Method `Delay(int frames)`** - A simple proxy to `Runner.Delay(frames)`, this allows child classes to simply write `await Delay(frames)`.
- **Method `RunTask(Func<Task> task)`** - A simple proxy to `Runner.RunTask(task)`, this allows child classes to simply write `await RunTask(...)`.

## Contributors &amp; Thanks

This library was the result of two years of me banging with rocks on the C# `async`/`await` model to make it do something it wasn't really meant to do, in the face of [really poor documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.0/task-types#builder-type) on how it actually works from Microsoft.  I tried to do this at least a dozen times before I finally figured out a way to make it work in April 2023.

I am indebted to [Oleksii Nikiforov](https://nikiforovall.medium.com/awaitable-awaiter-pattern-and-logical-micro-threading-in-c-4327f91d5923) and to [Bartosz Sypytkowski](https://gist.github.com/Horusiath/401ed16563dd442980de681d384f25b9) and to [Matthew Thomas](https://www.matthewathomas.com/programming/2021/09/30/async-method-builders-are-hard.html) for their hard work plumbing the depths of C# `async`/`await`.  And old versions of .NET Reflector really helped to untangle what was going on inside the generated code.

As implemented, this seems to cover most major use cases I can think of.  It has no bugs that I know of, but if you find one, feel free to report one.  (Note that the fact that Visual Studio cannot report logical GameTask stack frames is not a bug:  It's a useful but missing feature.)

Please feel free to use this library for any purpose you see fit, as per the terms of the [MIT Open-Source License](https://opensource.org/license/mit/).  (It's also a good case study for how to `async`/`await` can be made to do cooperative multitasking, which was nearly undocumented before I wrote this!)  I hope you find this useful, and it makes your code nicer and simpler!

-- Sean Werkema