using System;
using System.Reflection;

namespace OWML.LightBramble
{
	static class Extensions
	{
		public static void RaiseEvent(this object instance, string eventName, params object[] eventParams)
		{
			LightBramble.inst.DebugLog("Raising event");
			var type = instance.GetType();
			var eventField = type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
			if (eventField == null)
				LightBramble.inst.DebugLog($"Event with name {eventName} could not be found.");
			var multicastDelegate = eventField.GetValue(instance) as MulticastDelegate;
			if (multicastDelegate == null)
			{
				LightBramble.inst.DebugLog("multicastDelegate is null");
				return;
			}

			var invocationList = multicastDelegate.GetInvocationList();

			foreach (var invocationMethod in invocationList)
				invocationMethod.DynamicInvoke(eventParams);
		}
	}
}
