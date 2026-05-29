using System;
using System.Linq.Expressions;
using System.Reflection;

namespace ParalivesAPI.Core
{
    public static class ParalivesReflection
    {
        public static object ReadMember(object source, string name)
        {
            if (source == null || string.IsNullOrEmpty(name))
                return null;

            return ReadMember(source.GetType(), source, name);
        }

        public static bool TryReadMember<T>(object source, string name, out T value)
        {
            value = default(T);
            object raw = ReadMember(source, name);
            if (raw == null)
                return false;

            if (raw is T)
            {
                value = (T)raw;
                return true;
            }

            try
            {
                value = (T)Convert.ChangeType(raw, typeof(T));
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }

        public static ulong ReadUlong(object source, params string[] names)
        {
            if (source == null || names == null)
                return 0UL;

            for (int i = 0; i < names.Length; i++)
            {
                object value = ReadMember(source, names[i]);
                if (value == null)
                    continue;

                try
                {
                    return Convert.ToUInt64(value);
                }
                catch
                {
                }
            }

            return 0UL;
        }

        public static bool InvokeAny(object target, string[] methodNames, params object[] args)
        {
            object ignored;
            return InvokeAny(target, methodNames, out ignored, args);
        }

        public static bool InvokeAny(object target, string[] methodNames, out object result, params object[] args)
        {
            result = null;
            if (target == null || methodNames == null)
                return false;

            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!Contains(methodNames, method.Name) || !ParametersMatch(method.GetParameters(), args))
                    continue;

                result = method.Invoke(target, args);
                return true;
            }

            return false;
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

                Delegate handler = CreateForwardingDelegate(eventInfo.EventHandlerType, callback);
                eventInfo.AddEventHandler(target, handler);
                subscription = new EventSubscription(target, eventInfo, handler);
                return true;
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

                Delegate handler = CreateForwardingDelegate(parameters[0].ParameterType, callback);
                method.Invoke(target, new object[] { handler });
                subscription = new NoopSubscription();
                return true;
            }

            return false;
        }

        private static object ReadMember(Type type, object source, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property != null)
                return property.GetValue(source, null);

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (field != null)
                return field.GetValue(source);

            return null;
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
