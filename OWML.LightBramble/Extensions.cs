using OWML.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OWML.LightBramble
{
	static class Extensions
	{
		public static bool IsContainedIn<T>(this T source, IEnumerable<T> list)
		{
			if (source == null) throw new ArgumentNullException("IsContainedIn: source null");
			return list.Contains(source);
		}

		public static bool IsContainedIn<T>(this T source, params T[] list)
		{
			if (source == null) throw new ArgumentNullException("IsContainedIn: source null");
			return list.Contains(source);
		}

		public static bool IsInsideSphere(this AstroObject objectToTest, Vector3 sphereOrigin, float sphereRadius)
		{
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && objectToTest != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == objectToTest.transform.position)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool IsInsideSphere(this MonoBehaviour objectToTest, Vector3 sphereOrigin, float sphereRadius)
		{
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && objectToTest != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == objectToTest.transform.position)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool IsInsideSphere(this Vector3 objectToTest, Vector3 sphereOrigin, float sphereRadius)
		{
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && objectToTest != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == objectToTest)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool IsInBrambleSector<MonoBehaviour>(this MonoBehaviour obj)
		{
			var sector = obj.GetValue<Sector>("_sector");

			if (sector != null && sector.GetName().IsContainedIn(Sector.Name.BrambleDimension, Sector.Name.VesselDimension, Sector.Name.DarkBramble))
			{
				return true;
			}
			return false;
		}

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
