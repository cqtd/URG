﻿using System;

namespace UniRx.InternalUtil
{
	public class ThreadSafeQueueWorker
	{
		private const int MaxArrayLength = 0X7FEFFFFF;
		private const int InitialSize = 16;
		private Action<object>[] actionList = new Action<object>[InitialSize];

		private int actionListCount = 0;
		private object[] actionStates = new object[InitialSize];
		private bool dequing = false;

		private readonly object gate = new object();
		private Action<object>[] waitingList = new Action<object>[InitialSize];

		private int waitingListCount = 0;
		private object[] waitingStates = new object[InitialSize];

		public void Enqueue(Action<object> action, object state)
		{
			lock (gate)
			{
				if (dequing)
				{
					// Ensure Capacity
					if (waitingList.Length == waitingListCount)
					{
						int newLength = waitingListCount * 2;
						if ((uint) newLength > MaxArrayLength)
						{
							newLength = MaxArrayLength;
						}

						Action<object>[] newArray = new Action<object>[newLength];
						object[] newArrayState = new object[newLength];
						Array.Copy(waitingList, newArray, waitingListCount);
						Array.Copy(waitingStates, newArrayState, waitingListCount);
						waitingList = newArray;
						waitingStates = newArrayState;
					}

					waitingList[waitingListCount] = action;
					waitingStates[waitingListCount] = state;
					waitingListCount++;
				}
				else
				{
					// Ensure Capacity
					if (actionList.Length == actionListCount)
					{
						int newLength = actionListCount * 2;
						if ((uint) newLength > MaxArrayLength)
						{
							newLength = MaxArrayLength;
						}

						Action<object>[] newArray = new Action<object>[newLength];
						object[] newArrayState = new object[newLength];
						Array.Copy(actionList, newArray, actionListCount);
						Array.Copy(actionStates, newArrayState, actionListCount);
						actionList = newArray;
						actionStates = newArrayState;
					}

					actionList[actionListCount] = action;
					actionStates[actionListCount] = state;
					actionListCount++;
				}
			}
		}

		public void ExecuteAll(Action<Exception> unhandledExceptionCallback)
		{
			lock (gate)
			{
				if (actionListCount == 0)
				{
					return;
				}

				dequing = true;
			}

			for (int i = 0; i < actionListCount; i++)
			{
				Action<object> action = actionList[i];
				object state = actionStates[i];
				try
				{
					action(state);
				}
				catch (Exception ex)
				{
					unhandledExceptionCallback(ex);
				}
				finally
				{
					// Clear
					actionList[i] = null;
					actionStates[i] = null;
				}
			}

			lock (gate)
			{
				dequing = false;

				Action<object>[] swapTempActionList = actionList;
				object[] swapTempActionStates = actionStates;

				actionListCount = waitingListCount;
				actionList = waitingList;
				actionStates = waitingStates;

				waitingListCount = 0;
				waitingList = swapTempActionList;
				waitingStates = swapTempActionStates;
			}
		}
	}
}