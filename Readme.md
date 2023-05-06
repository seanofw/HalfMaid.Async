# HalfMaid.Async

Copyright &copy; 2023 by Sean Werkema

Licensed under the [MIT open-source license](https://opensource.org/license/mit/)


----------


## Contents

- [Overview](#overview)
- [Installation](#installation)
- [Example &amp; Rationale](#example--rationale)
	- [The Problem](#the-problem)
	- [The Ideal](#the-ideal)
	- [The Solution](#the-solution)
	- [Running It](#running-it)
- [Usage](#usage)
	- [GameTasks](#gametasks)
		- [Detailed Samples](#detailed-samples)
		- [Complete Example](#complete-example)
	- [Starting GameTasks](#starting-gametasks)
	- [Main Loop](#main-loop)
	- [AsyncGameObjectBase](#asyncgameobjectbase)
	- [External Tasks](#external-tasks)
	- [Bulk Cancellation](#bulk-cancellation)
- [APIs](#apis)
	- [GameTask](#gametask)
	- [GameTask\<T\>](#gametaskt)
	- [GameTaskRunner](#gametaskrunner)
	- [AsyncGameObjectBase](#asyncgameobjectbase-1)
	- [GameTaskYieldAwaitable](#gametaskyieldawaitable)
	- [ExternalTaskAwaitable](#externaltaskawaitable)
	- [GameTaskAwaiter](#gametaskawaiter)
	- [GameTaskAwaiter\<T\>](#gametaskawaitert)
- [FAQ](#faq)
- [Contributors & Thanks](#contributors--thanks)


----------


## Overview

This repository contains the HalfMaidGames Async library, which is designed to solve a common problem in video-game programming in C#:  The difficulty of building video-game state machines.

Instead of using `switch`-statements or hacking `IEnumerable` generators for your state machines, you can use nice, clean `async`/`await`-based programming for each actor in your game, and it scales very well to complex use cases.

Importantly, even though this uses `async`/`await`, all of your tasks will always run on a single thread:  This library carefully uses `async`/`await` to time-slice a single thread, just like you would write using `switch` statements or `IEnumerable` generators, but with much simpler code.  This does not use the thread pool, and it does not trigger work in other threads:  You are in total control of what gets run and where it gets run, the behavior you need for the update cycle in a game loop.


----------


## Installation

You can install the latest [HalfMaidGames.Async library]() as a Nuget package.

The package is multi-targeted for .NET Core 2.1, .NET Core 3.1, .NET 5.0, and .NET 6.0+ to provide maximum backward compatibility.  .NET Framework 4.x and earlier and .NET Core 1.x are not supported.


----------


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
In short, the only methods you really need to know on the runner are `StartImmediately()`, which starts running an `async` method, and `RunNextFrame()`, which runs anything that needs to run for the next frame.


----------


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


----------


## APIs

There are relatively few public APIs, as the library mostly relies on standard `async`/`await` mechanics to function.  But here are the ones that are exposed:

### GameTask

This is a `class` that represents an active task for a function that otherwise would return `void`.  It may be executed via normal `await`/`async`.

This is combined with its own builder type to keep heap overhead as low as possible.  It consists of about 5 or 6 pointers' worth of data.

`GameTask` may be safely copied and moved around, since it is only a reference to a class and some additional methods.

Do not attempt to instantiate a `GameTask()` yourself:  `GameTask.Create()` should only be called by the C# compiler.

- **Property `GameTaskStatus Status`**:  The current status of this task, either `InProgress`, `Success` (completed without an exception), or `Failed` (threw an exception).

- **Property `bool IsCompleted`**: True if this task has ended (via normal completion or an exception), false if it is still `InProgress`.  Required by the C# compiler.

- **Property `GameTask Task`**: A reference to this same class.  Required by the C# compiler.

- **Property `Exception Exception`**: If an exception was thrown by this task, this is the exception.  May be `null`.

- **Property `ExceptionDispatchInfo ExceptionDispatchInfo`**: If an exception was thrown by this task, this is its captured dispatch information, which allows it to be re-thrown with a correct stack trace.  May be `null`.

- **Static method `Create()`**:  Creates a new `GameTask()`.  Do not call this; it will be called automatically by the C# compiler's generated code as necessary.

- **Method `Start<TStateMachine>(ref TStateMachine)`**: Start the given state machine.  Required by the C# compiler.  Do not call this directly.

- **Method `SetStateMachine(IAsyncStateMachine)`**: Switch state machines.  Required by the C# compiler, and deprecated.  Do not call this directly.

- **Method `SetException(Exception)`**: Notify this task that an exception has been raised.  Required by the C# compiler.  Do not call this directly, or you _will_ break the task.

- **Method `SetResult()`**: Notify this task that it has completed successfully.  Required by the C# compiler.  Do not call this directly, or you _will_ break the task.

- **Method `AwaitOnCompleted<TWaiter, TStateMachine>(ref TWaiter, ref TStateMachine)`**: Tell the given task how to continue after a wait completes, which is to invoke the next phase of the given state machine.  Do not call this directly, or you _will_ break the task.

- **Method `AwaitUnsafeOnCompleted<TWaiter, TStateMachine>(ref TWaiter, ref TStateMachine)`**: Tell the given task how to continue after a wait completes, which is to invoke the next phase of the given state machine.  This version can avoid switching environments, but is the currently same as `AwaitOnCompleted()`.  Do not call this directly, or you _will_ break the task.

- **Method `GetAwaiter()`**: Returns a `GameTaskAwaiter` that can be used by `await` to trigger any continued computation in this task.  You generally do not need to call this.

This class has many public methods that are required to implement the `AsyncMethodBuilder` pattern.  Even though they are declared `public`, they should only be invoked by the C# compiler itself.  As a general rule, don't touch any part of a `GameTask` other than its public properties.

This class is _not_ thread-safe.

### GameTask\<T\>

This is a similar `class` to `GameTask`, and most of the above description applies.  This inherits from `GameTask`.  It also has the following notable changes:

- **Property `T Result`**: The result (return value) of this task after it has successfully completed.  Will be `default(T)` until the task successfully completes.

- **Method `SetResult(T)`**: Notify this task that it has completed successfully and returned a `T`.  Required by the C# compiler.  Do not call this directly, or you _will_ break the task.

- **Method `GetAwaiter()`**: Returns a `GameTaskAwaiter<T>` that can be used by `await` to trigger any continued computation in this task.  You generally do not need to call this.

This class is _not_ thread-safe.

### GameTaskRunner

This manages the active state of a group of tasks, and can run those tasks forward to a specific point in time, either one frame, several frames, or all frames.

This class is _not_ thread-safe except where noted below.

- **Constructor `GameTaskRunner()`** - Construct a new runner.  No parameters are required.

- **Property `TaskCount`** - This returns a count of how many `InProgress` `GameTask`s are being tracked by the runner.  When this count reaches zero, all `GameTask`s have either completed successfully or thrown exceptions, and none have any remaining work.  This property is thread-safe, and may be queried by any thread at any time.  (Note, however, that it is point-in-time information, so it may change immediately after you read it!)

- **Method `EnqueueFuture(Action action, int frames)`** - Enqueue an action to occur at some point in the future (the current time plus the given number of frames), during `RunNextFrame()`.  This call is thread-safe, and is a way for an external thread to push work onto the runner's thread.

- **Method `Next()`** - This returns an awaitable that resolves during the next frame of execution.  It should always be called as `await Next()`.  It is conceptually similar to `await Task.Yield()`.

- **Method `Delay(int frames)`** - This returns an awaitable that resolves in a future frame of execution.  It should always be called as `await Delay(frames)`.  It is conceptually similar to `await Task.Delay(msec)`.

- **Method `StartImmediately(Func<GameTask> action)`** - This causes the given action to be started (run/called/invoked) immediately; if it encounters an `await` during its execution that would cause it to block, its continuation will be registered with the runner, and then this call will return.  This is conceptually similar to `Task.Run(action)`.

- **Method `StartYielded(Func<GameTask> action)`** - This causes the given action to be started (run/called/invoked) during the next frame, and returns immediately.  This is conceptually similar to a pattern like `Task.Run(async () => { await Task.Yield(); await action(); })`. This method is thread-safe, and is a way for an external thread to push work onto the runner's thread.

- **Method `RunUntilAllTasksFinish()`** - This executes all remaining registered tasks in a tight loop until all `GameTask`s and external `Task`s have either finished successfully or thrown exceptions, and then it returns.  This should be used at the end of your program (or of the `GameTaskRunner`'s lifetime) to ensure that any `finally` or `using` statements within any active tasks are eventually properly completed.

- **Method `RunNextFrame()`** - Run exactly one subsequent frame's worth of execution for any registered tasks.  As soon as all tasks have either completed or have invoked `Next()` or `Delay()` to wait for a subsequent frame, this method returns.

- **Method `RunTask(Func<Task> task)`** - Allow a traditional I/O task to be executed and managed by the task runner.  The `Task` will be executed by the thread pool, but will be resumed on the original thread.

- **Method `CancelAllTasks<TException>(Func<TException> createException, Action<Action>? handleUncaughtExceptions)`** - Cancel all active GameTasks by raising exceptions inside them.  You can provide an optional custom function to create the exceptions.  You can provide an optional custom handler for any uncaught exceptions.  If a creation function is not provided, this will create `TaskCanceledException`s on its own.

### AsyncGameObjectBase

This is a convenience class.  You do not need to inherit from it, but doing so can simplify calling methods like `GameTaskRunner.Next()` in your own code.

- **Static property `GameTaskRunner Runner`** - The runner that will be used by this object.  This is initialized by default to `new GameTaskRunner()`.

- **Method `Next()`** - A simple proxy to `Runner.Next()`, this allows child classes to simply write `await Next()`.

- **Method `Delay(int frames)`** - A simple proxy to `Runner.Delay(frames)`, this allows child classes to simply write `await Delay(frames)`.

- **Method `RunTask(Func<Task> task)`** - A simple proxy to `Runner.RunTask(task)`, this allows child classes to simply write `await RunTask(...)`.

This class is nothing but proxies to `GameTaskRunner`, so it has the same thread-safety rules that the runner has.

### GameTaskYieldAwaitable

This is the `struct` type returned by `runner.Next()` and `runner.Delay()`.  It is an "awaitable" type, which is a pattern-based — not inheritance-based — concept.  It is `readonly`, and may be safely copied by value.  You generally do *not* need to interact with this directly, and it is only included here for completeness.

- **Field `GameTaskRunner Runner`** - The runner that will continue this awaitable in a subsequent frame.

- **Field `int FrameCount`** - The number of frames that should elapse before this awaitable should continue, `1` for a call to `Next()`, and identical to the value passed into `Delay(frames)`.

- **Property `bool IsCompleted`** - Whether this awaitable has been continued.  Required by the C# compiler.  Always returns false.

- **Method `GetAwaiter()`** - Returns this object.  Required by the C# compiler.

- **Method `OnCompleted(continuation)`** - Registers work to be performed when this awaitable completes. Required by the C# compiler.

- **Method `GetResult()`** - Called when the awaitable completes.  Required by the C# compiler.  May raise an exception if the awaitable failed to complete or was cancelled.

### ExternalTaskAwaitable

This is the `class` type returned by `runner.RunTask()`.  It is an "awaitable" type, which is a pattern-based — not inheritance-based — concept.  It is immutable.  You generally do *not* need to interact with this directly, and it is only included here for completeness.

- **Field `GameTaskRunner Runner`** - The runner that will continue this awaitable in a subsequent frame.

- **Property `bool IsCompleted`** - Whether this awaitable has been continued.  Required by the C# compiler.  Always returns false.

- **Method `GetAwaiter()`** - Returns this object.  Required by the C# compiler.

- **Method `OnCompleted(continuation)`** - Registers work to be performed when this awaitable completes. Required by the C# compiler.

- **Method `GetResult()`** - Called when the awaitable completes.  Required by the C# compiler.

### GameTaskAwaiter

This `struct` is returned by `GameTask.GetAwaiter()` and is used to wait for the completion of the `GameTask`.  It is `readonly`.  It contains very little:

- **Field `GameTask Task`**: A reference to the task to wait for.
- **Property `bool IsCompleted`**: Whether the `GameTask` has completed or not. Required by the C# compiler.
- **Constructor `GameTaskAwaiter(GameTask)`**: Construct a new awaiter for the given task.
- **Method `GetResult()`**: Called automatically by the C# compiler's generated code to notify the awaiter that the `await` has completed.  Required by the C# compiler.  Do not call this directly.
- **Method `OnCompleted(Action)`**: Called by the C# compiler's generated code to register a continuation to execute after the task completes.  Required by the C# compiler.  Do not call this directly.
- **Method `UnsafeOnCompleted(Action)`**: Called by the C# compiler's generated code to register a continuation to execute after the task completes, in situations where the execution and synchronization contexts do not need to change.  Required by the C# compiler.  Do not call this directly.

In short, you shouldn't invoke this directly, and you will probably never notice it exists.

### GameTaskAwaiter<T>

This is nearly identical to the `struct` above, but designed for a `GameTask<T>` instead.  It does not inherit from `GameTaskAwaiter` because `struct` types cannot inherit.

As with `GameTaskAwaiter`, you shouldn't invoke this directly, and you will probably never notice it exists.


----------


## FAQ

- **What's the difference between `Task` and `GameTask`?**

	The standard `Task` object is designed around the thread pool:  It's intended to be a lot like threading, but easier, and with better performance.  `Task` generally uses the C# `ThreadPool` for scheduling, and will use it for any situation when it needs to execute something and doesn't know where else to put it.

	`GameTask` is similar in some ways, but is designed not just to fit the resource constraints of a video game, but to embody a very different concept, that of time-slicing a *single* thread.  In a video game, you need to be certain of what will and won't execute during the current frame.  You need to know that X object will run a certain chunk of code _in the current thread_ and then _stop_ and then wait to be told to continue in the next frame.
	
	This library is designed to make that kind of predictable time-slicing easy, but still using object-oriented programming and functional-programming, and not switching to alternative programming models like [ECS](https://en.wikipedia.org/wiki/Entity_component_system).

- **Can I call child methods within an `async GameTask` method?**

	Sure!  That's part of the point, and part of why using `async`/`await` is better than using `switch`-based state machines:  You can call deeper and deeper, and organize your code using normal software-engineering principles.
	
	So just like with `Task`-based async, the child methods must be declared `async GameTask` too if they need to invoke `runner.Next()` or `runner.Delay()` or `runner.RunTask()`, and you'll also need to `await` them.
	
	Of course, if they *don't* need to wait for a frame or a result, you can just call them directly.

- **What about memory overhead?  How expensive is a `GameTask`?**

	A `GameTask` is not substantially more expensive than an `IEnumerable`/`yield` pattern.  Each `async` method has two objects on the heap to represent it:  The first is a state machine (`IAsyncStateMachine`) which stores both its code state (i.e., an integer representing which code to run next) and its data state (its local variables).  The second object is a `GameTask`, which provides sufficient information to pause and resume the .NET runtime from a paused `await` in the state machine.  A `GameTask` contains about 5 to 6 pointers' worth of data (~24 bytes on a 32-bit CPU, ~48 bytes on a 64-bit CPU).

	If you were to hand-implement the state machine using a `switch` statement, you would likely have an equivalent of the first object to represent both the code and data state, and no equivalent of the second object.

	Either way, a `GameTask` is not hugely expensive:  It is a single extra object, measured in bytes, not kilobytes.  It is allocated the first time a method is entered, and garbage-collected when the method completes, and exists for the full lifetime in between.  No matter how many `await` invocations the method contains, the same `GameTask` is used for the full dynamic extent of the method.

- **What about CPU overhead?  How slow is `async`/`await`?**

	The `async`/`await` mechanics will likely be slower than a hand-implemented `switch` statement, which will likely be slower than a bulk-update system like ECS.  Pausing and resuming a method is not free.

	_However_, that overhead is still measured in nanoseconds:  You can have tens if not hundreds of thousands of `GameTask`s updating in a single frame and still meet 60 FPS.
	
	Moreover, because `RunNextFrame()` uses an internal priority-queue-based scheduler, any `GameTask` that is waiting on `runner.Delay()` or `runner.RunTask()` will have *zero* cost until that `GameTask` finally resumes.  You can have hundreds of thousands of "sleeping" objects, and if only one wakes up per frame, you pay little more CPU than the cost of its execution.

- **Do exceptions work inside an `async GameTask`?**

	Exceptions are fully supported:  If exceptions get raised, you `try`/`catch`/`finally` them just like you would anywhere else in your code, and an outer `try`/`catch`/`finally` can catch exceptions from deep inside an `async`/`await` call stack.

	The stack trace of the exception will show the full _logical_ call stack to get to where it was thrown:  Even if it was thrown many frames after the outermost `async` method was invoked, the outermost `async` method will still appear in the stack trace.

- **Does `using` work inside an `async GameTask`?**

	Just like exceptions, this works like you think it should.  A `using` with an `await` in the middle will invoke `Dispose()` when the method finally completes, even if that's many frames in the future.

- **Do I need to inherit my objects from `AsyncGameObjectBase`?**

	No!  You can have your own inheritance hierarchies.  I include this for convenience, not because it's required.  The entire source code (minus comments) for `AsyncGameObjectBaase` is presented below to show you how simple it is and how easy it is to choose to use it or not use it:

	```cs
	public abstract class AsyncGameObjectBase
	{
		public static GameTaskRunner Runner { get; set; } = new GameTaskRunner();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected AsyncGameObjectBase()
		{
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable Next() => Runner.Next();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable Delay(int frames) => Runner.Delay(frames);

		public ExternalTaskAwaitable RunTask(Func<Task> task) => Runner.RunTask(task);
	}
	```

	As you can see, there's very little to it, and it does nothing more than forward calls to the `GameTaskRunner` class.  You can copy-and-paste the above methods into your own base class if you have a custom inheritance hierarchy but still want the convenience of being able to simply write `await Next();` in your code.

- **Can I have more than one `GameTaskRunner`?  Is anything `static`?**

	You can have as many `GameTaskRunner` instances as you want; each will run the `async GameTask` methods started inside it.  Nothing is declared `static` in the entire library except for a `static` runner instance in the `AsyncGameObjectBase` class, which is only included to make simple use cases easy:  You are not required to use it.
	
	It may even be useful in some cases to have multiple `GameTaskRunner` instances:  For example, one to manage enemies in the main gameplay, and another to manage actions in, say, a popup menu only while it's open.

	It's up to you to decide how many or how few runners you need.

- **What about thread safety?**

	`GameTask` and `GameTaskRunner` are not thread-safe.  Some parts of `GameTaskRunner` are, but not all of it is.  You should only ever use a `GameTask` or `GameTaskRunner` in the thread that created it, with three notable exceptions to this rule:

	- `runner.StartYielded(...gameTask...)` can be safely called by other threads to start a `GameTask` on the runner's thread.

	- `runner.TaskCount` can safely be queried from any thread.

	- `await runner.RunTask(...task...)` will kick off the given `Task` on the thread pool:  That `Task` will run in parallel to the thread that started it, possibly on another CPU core.  However, when the `await` completes, it will resume back on the original calling thread during `RunNextFrame()`.

	Do not assume any other methods on `GameTaskRunner` are thread-safe.

- **How do I safely clean up after `async GameTask`s?**

	You need to make sure that you clean up your tasks when you're done with running them, or `try`/`catch`/`finally` and `using` and `runner.RunTask()` may not work correctly inside them.
	
	"Done" in this context means that you're not going to use this `GameTaskRunner` anymore, either because you've made a major transition in your code (i.e., main menu --> gameplay) where the previous tasks don't matter anymore, or because you're exiting the game.  It's up to you to decide when "done" happens.

	When you're done with a runner and all of the `GameTask`s inside it, call `runner.RunUntilAllTasksFinish()`.  This will ensure that every task has fully completed before it returns, and it will block until no more tasks remain.

	This is also why `runner.CancelAllTasks()` exists:  It lets you throw an exception inside each task, which can be used to shut them down more cleanly than simply dropping them on the floor.  Make sure to `catch` in your `GameTask` code whatever exception you raise, though, if you want your task to know it's being killed!

	Typically, you'll want to use a pattern like this to shut everything down cleanly:

	```cs
	public void ExitMyGame()
	{
		runner.CancelAllTasks();
		runner.RunUntilAllTasksFinish();
	}
	```

	The first call will attempt to exit every task reasonably cleanly, and the second call won't continue until everything definitely *has* exited.

	The `GameTaskRunner` does not implement `IDisposable`; if you want `Dispose()`-like behavior, you can implement it yourself by calling those two methods as shown above.

	If you don't *care* that `try`/`catch`/`finally` and `using` and `runner.RunTask()` may not finish inside your `GameTask`s, you *can* always skip the `RunUntilAllTasksFinish()` step, and just let GC collect both the runner and the `GameTask`s when it wants to, but that often requires care not to use those language features.

- **What about debugging?**

	When debugging C# code that uses `async GameTask`, there are a few important points to be aware of:

	- Stepping over an `await` may produce weird results, because you may not reach the other side of it until many frames later.  It is better to set a breakpoint below it than to try to step over it in a debugger.

	- The debugger call stack will typically show the *real* call stack and will be very shallow, only showing the currently-executing innermost state machine:  `GameLoop()` --> `runner.RunNextFrame()` --> `DeepAsyncMethod()`.  In the future, I may try for better debugger integration, but for now, don't be surprised by the debugger's call stack being unhelpful.

	- To find out the *logical* call stack, you can throw an exception and immediately catch it:

		```cs
		private async GameTask DeepAsyncMethod()
		{
			...
			await runner.Next();
			...
			try { throw Exception(); }
			catch (Exception e)
			{
				// Set a breakpoint on the line below.
				string logicalStackTrace = e.StackTrace;
			}
		}
		```
		In the above example, the `logicalStackTrace` will show which `async GameTask` methods were called en route to arrive at `DeepAsyncMethod()`.

- **Which .NET is this compatible with?  Why are there versions for so many different .NET releases?**

	Because each .NET release supports different functionality, and I use conditional compilation to support that added functionality where possible:

	- .NET Core 2.x and 3.x, and .NET 5 use more-or-less the same build.  Separate versions are included because each platform optimizes the code slightly differently.

	- .NET 6 provides a new `PriorityQueue<T,S>` class, which I use on the newer platforms where it exists.  (To support older .NET Core and .NET 5, this library contains its own hacked copy of that `PriorityQueue<T,S>` class.)  This build should be compatible with .NET 7 and .NET 8 as well.

	- .NET Framework 4.x is _not_ supported and will not be supported, because even though it supports `async`/`await`, it does not include `AsyncMethodBuilder`, which is required for custom task types like `GameTask` to work.

	- .NET Core 1.x is not supported and is too niche to support.  Consider upgrading to a newer .NET if you're on .NET Core 1.x.


----------


## Contributors & Thanks

This library was the result of two years of me banging with rocks on the C# `async`/`await` model to make it do something it wasn't really meant to do, in the face of [really poor documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.0/task-types#builder-type) on how it actually works from Microsoft.  I tried to do this at least a dozen times before I finally figured out the core of how to get it to work in April 2023, with critical enhancements in May 2023.

I am indebted to [Oleksii Nikiforov](https://nikiforovall.medium.com/awaitable-awaiter-pattern-and-logical-micro-threading-in-c-4327f91d5923) and to [Bartosz Sypytkowski](https://gist.github.com/Horusiath/401ed16563dd442980de681d384f25b9) and to [Matthew Thomas](https://www.matthewathomas.com/programming/2021/09/30/async-method-builders-are-hard.html) for their hard work plumbing the depths of C# `async`/`await`.  Old versions of .NET Reflector really helped to untangle what was going on inside the early `async`/`await` generated code too.

I would also like to thank Microsoft as well for releasing the .NET code under an open-source license so it could be studied.  Without being able to read through `Task.cs` a few hundred times, I don't think I'd have been able to pull this off.

As implemented, this seems to cover most major use cases I can think of.  It has no bugs that I know of, but if you find one, please feel free to report one.  (Note that the fact that Visual Studio cannot report logical GameTask stack frames is not a bug:  It's a useful but missing feature.)

Please feel free to use this library for any purpose you see fit, as per the terms of the [MIT Open-Source License](https://opensource.org/license/mit/).  (It's also a good case study for how to `async`/`await` can be made to do cooperative multitasking, which was nearly undocumented before I wrote this!)

I hope you find this useful, and find that it makes your code nicer and simpler!

-- Sean Werkema