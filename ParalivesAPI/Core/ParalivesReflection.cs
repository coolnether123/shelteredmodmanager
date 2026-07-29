using System;
using System.Linq.Expressions;
using System.Reflection;

namespace ParalivesAPI.Core
{
    public static class ParalivesReflection
    {
        private static readonly ParalivesCompatibilityFacade Compatibility = new ParalivesCompatibilityFacade();

        public static object ReadMember(object source, string name)
        {
            object value;
            return Compatibility.TryReadMember<object>(source, name, out value) ? value : null;
        }

        public static bool TryReadMember<T>(object source, string name, out T value)
        {
            return Compatibility.TryReadMember<T>(source, name, out value);
        }

        public static ulong ReadUlong(object source, params string[] names)
        {
            return Compatibility.ReadGuidOrDefault(source, 0UL, names);
        }

        public static bool InvokeAny(object target, string[] methodNames, params object[] args)
        {
            object ignored;
            return InvokeAny(target, methodNames, out ignored, args);
        }

        public static bool InvokeAny(object target, string[] methodNames, out object result, params object[] args)
        {
            return Compatibility.TryCallAllowValueTypeReturn<object>(target, out result, methodNames, args);
        }

        public static bool TrySubscribe(
            object target,
            string[] eventNames,
            string[] subscribeMethodNames,
            Action<object> callback,
            out IDisposable subscription)
        {
            subscription = null;
            if (target == null || callback == null)
                return false;

            if (TrySubscribeEvent(target, eventNames, callback, out subscription))
                return true;

            return TrySubscribeMethod(target, subscribeMethodNames, callback, out subscription);
        }

        private static bool TrySubscribeEvent(
            object target,
            string[] eventNames,
            Action<object> callback,
            out IDisposable subscription)
        {
            subscription = null;
            if (eventNames == null)
                return false;

            Type type = target.GetType();
            for (int i = 0; i < eventNames.Length; i++)
            {
                EventInfo eventInfo = type.GetEvent(eventNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (eventInfo == null)
                    continue;

                try
                {
                    Delegate handler = CreateForwardingDelegate(eventInfo.EventHandlerType, callback);
                    eventInfo.AddEventHandler(target, handler);
                    subscription = new EventSubscription(target, eventInfo, handler);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool TrySubscribeMethod(
            object target,
            string[] methodNames,
            Action<object> callback,
            out IDisposable subscription)
        {
            subscription = null;
            if (methodNames == null)
                return false;

            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!Contains(methodNames, method.Name))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || !typeof(Delegate).IsAssignableFrom(parameters[0].ParameterType))
                    continue;

                try
                {
                    Delegate handler = CreateForwardingDelegate(parameters[0].ParameterType, callback);
                    method.Invoke(target, new object[] { handler });
                    subscription = new NoopSubscription();
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static Delegate CreateForwardingDelegate(Type delegateType, Action<object> callback)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            ParameterExpression[] expressions = new ParameterExpression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                expressions[i] = Expression.Parameter(parameters[i].ParameterType, parameters[i].Name);

            Expression argument;
            if (expressions.Length >= 2)
                argument = Expression.Convert(expressions[1], typeof(object));
            else if (expressions.Length == 1)
                argument = Expression.Convert(expressions[0], typeof(object));
            else
                argument = Expression.Constant(null, typeof(object));

            MethodInfo forward = typeof(Action<object>).GetMethod("Invoke");
            MethodCallExpression body = Expression.Call(Expression.Constant(callback), forward, argument);
            return Expression.Lambda(delegateType, body, expressions).Compile();
        }

        private static bool ParametersMatch(ParameterInfo[] parameters, object[] args)
        {
            if (args == null)
                args = new object[0];
            if (parameters.Length != args.Length)
                return false;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                    continue;
                if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    return false;
            }

            return true;
        }

        private static bool Contains(string[] values, string value)
        {
            if (values == null || value == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private sealed class EventSubscription : IDisposable
        {
            private object _target;
            private EventInfo _eventInfo;
            private Delegate _handler;

            public EventSubscription(object target, EventInfo eventInfo, Delegate handler)
            {
                _target = target;
                _eventInfo = eventInfo;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_target != null && _eventInfo != null && _handler != null)
                    _eventInfo.RemoveEventHandler(_target, _handler);

                _target = null;
                _eventInfo = null;
                _handler = null;
            }
        }

        private sealed class NoopSubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
