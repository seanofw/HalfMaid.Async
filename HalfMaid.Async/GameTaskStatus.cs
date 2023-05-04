namespace HalfMaid.Async
{
	/// <summary>
	/// Possible statuses of each game task.
	/// </summary>
	public enum GameTaskStatus
	{
		/// <summary>
		/// Currently running, or waiting for a subsequent frame.
		/// </summary>
		InProgress = 0,

		/// <summary>
		/// Finished successfully.
		/// </summary>
		Success,

		/// <summary>
		/// Threw an exception and was aborted.
		/// </summary>
		Failed,

		/// <summary>
		/// Cancelled by a custom exception.
		/// </summary>
		Cancelled,
	}
}
