using System;
using System.Collections.Generic;

namespace ShelteredAPI.Interactions
{
    /// <summary>
    /// Placement strategy for inserting a custom interaction into a Sheltered object's interaction list.
    /// </summary>
    public enum ObjectButtonInsertMode
    {
        Last,
        First,
        Index,
        Before,
        After,
        Priority,
        CustomIndex
    }

    /// <summary>
    /// Context passed to custom interaction index resolvers.
    /// Use it to inspect the target object and current vanilla interaction order before choosing an index.
    /// </summary>
    public sealed class ObjectButtonInsertContext
    {
        private readonly Obj_Base _targetObject;
        private readonly IList<string> _currentInteractions;
        private readonly string _interactionName;

        internal ObjectButtonInsertContext(Obj_Base targetObject, IList<string> currentInteractions, string interactionName)
        {
            _targetObject = targetObject;
            _currentInteractions = currentInteractions;
            _interactionName = interactionName;
        }

        public Obj_Base TargetObject { get { return _targetObject; } }
        public IList<string> CurrentInteractions { get { return _currentInteractions; } }
        public string InteractionName { get { return _interactionName; } }
    }

    /// <summary>
    /// Fluent builder scoped to one Sheltered object type.
    /// Use the Add methods to add one or more interaction buttons for that target type.
    /// </summary>
    public sealed class ObjectButtonTargetBuilder
    {
        private readonly ObjectManager.ObjectType _targetType;

        internal ObjectButtonTargetBuilder(ObjectManager.ObjectType targetType)
        {
            _targetType = targetType;
        }

        public ObjectButtonBuilder Add<TInteraction>(string interactionName) where TInteraction : Int_Base
        {
            return Add(interactionName, typeof(TInteraction));
        }

        public ObjectButtonBuilder Add(string interactionName, Type interactionType)
        {
            return new ObjectButtonBuilder(this, _targetType, interactionName, interactionType);
        }

        public ObjectManager.ObjectType TargetType { get { return _targetType; } }
    }

    /// <summary>
    /// Fluent builder for one custom object interaction.
    /// Configure placement, predicates, diagnostics, and callbacks before calling <see cref="Register"/>.
    /// </summary>
    public sealed class ObjectButtonBuilder
    {
        private readonly ObjectButtonTargetBuilder _parent;
        private readonly ObjectManager.ObjectType _targetType;
        private readonly string _interactionName;
        private readonly Type _interactionType;

        private ObjectButtonInsertMode _mode = ObjectButtonInsertMode.Last;
        private string _anchorInteraction;
        private int _index = -1;
        private int _priority = int.MaxValue;
        private Func<ObjectButtonInsertContext, int> _customIndexResolver;
        private Func<Obj_Base, bool> _predicate;
        private Action<Obj_Base, Int_Base> _onInjected;
        private bool _debugEnabled;
        private string _debugKey;
        private int _debugMaxLogs = 40;

        internal ObjectButtonBuilder(
            ObjectButtonTargetBuilder parent,
            ObjectManager.ObjectType targetType,
            string interactionName,
            Type interactionType)
        {
            _parent = parent;
            _targetType = targetType;
            _interactionName = interactionName;
            _interactionType = interactionType;
        }

        public ObjectButtonBuilder First()
        {
            _mode = ObjectButtonInsertMode.First;
            return this;
        }

        public ObjectButtonBuilder Last()
        {
            _mode = ObjectButtonInsertMode.Last;
            return this;
        }

        public ObjectButtonBuilder AtIndex(int index)
        {
            _mode = ObjectButtonInsertMode.Index;
            _index = index;
            return this;
        }

        public ObjectButtonBuilder Before(string interactionType)
        {
            _mode = ObjectButtonInsertMode.Before;
            _anchorInteraction = interactionType;
            return this;
        }

        public ObjectButtonBuilder After(string interactionType)
        {
            _mode = ObjectButtonInsertMode.After;
            _anchorInteraction = interactionType;
            return this;
        }

        public ObjectButtonBuilder WithPriority(int priority)
        {
            _mode = ObjectButtonInsertMode.Priority;
            _priority = priority;
            return this;
        }

        public ObjectButtonBuilder UsingIndexResolver(Func<ObjectButtonInsertContext, int> resolver)
        {
            _mode = ObjectButtonInsertMode.CustomIndex;
            _customIndexResolver = resolver;
            return this;
        }

        public ObjectButtonBuilder When(Func<Obj_Base, bool> predicate)
        {
            _predicate = predicate;
            return this;
        }

        public ObjectButtonBuilder OnInjected(Action<Obj_Base, Int_Base> callback)
        {
            _onInjected = callback;
            return this;
        }

        public ObjectButtonBuilder Debug(bool enabled)
        {
            _debugEnabled = enabled;
            if (!enabled) _debugKey = null;
            return this;
        }

        public ObjectButtonBuilder Debug(string debugKey, int maxLogs)
        {
            _debugEnabled = true;
            _debugKey = debugKey;
            _debugMaxLogs = maxLogs > 0 ? maxLogs : 40;
            return this;
        }

        public ObjectButtonTargetBuilder Register()
        {
            ShelteredAPI.Interactions.InteractionButtonBuilder builder =
                ShelteredAPI.Interactions.InteractionRegistry.For(_targetType).Add(_interactionName, _interactionType);

            switch (_mode)
            {
                case ObjectButtonInsertMode.First:
                    builder.First();
                    break;
                case ObjectButtonInsertMode.Last:
                    builder.Last();
                    break;
                case ObjectButtonInsertMode.Index:
                    builder.AtIndex(_index);
                    break;
                case ObjectButtonInsertMode.Before:
                    builder.Before(_anchorInteraction);
                    break;
                case ObjectButtonInsertMode.After:
                    builder.After(_anchorInteraction);
                    break;
                case ObjectButtonInsertMode.Priority:
                    builder.WithPriority(_priority);
                    break;
                case ObjectButtonInsertMode.CustomIndex:
                    builder.UsingIndexResolver(delegate(ShelteredAPI.Interactions.InteractionInsertContext ctx)
                    {
                        if (_customIndexResolver == null)
                            return ctx != null && ctx.CurrentInteractions != null ? ctx.CurrentInteractions.Count : 0;

                        ObjectButtonInsertContext wrapped = new ObjectButtonInsertContext(
                            ctx != null ? ctx.TargetObject : null,
                            ctx != null ? ctx.CurrentInteractions : null,
                            ctx != null ? ctx.InteractionName : _interactionName);
                        return _customIndexResolver(wrapped);
                    });
                    break;
            }

            if (_predicate != null) builder.When(_predicate);
            if (_onInjected != null) builder.OnInjected(_onInjected);
            if (_debugEnabled)
            {
                if (!string.IsNullOrEmpty(_debugKey)) builder.Debug(_debugKey, _debugMaxLogs);
                else builder.Debug(true);
            }

            builder.Register();
            return _parent;
        }
    }

    /// <summary>
    /// Fluent helper for injecting interaction-menu buttons into Sheltered objects.
    /// Prefer this over direct patching when adding a new object action from a mod.
    /// </summary>
    public static class ObjectButtonInjector
    {
        public static ObjectButtonTargetBuilder For(ObjectManager.ObjectType targetType)
        {
            return new ObjectButtonTargetBuilder(targetType);
        }

        public static ObjectButtonTargetBuilder On(ObjectManager.ObjectType targetType)
        {
            return For(targetType);
        }

        public static ObjectButtonBuilder AddButton<TInteraction>(ObjectManager.ObjectType targetType, string interactionName)
            where TInteraction : Int_Base
        {
            return For(targetType).Add<TInteraction>(interactionName);
        }

        public static void Register(ObjectManager.ObjectType targetType, string interactionName, Type interactionType)
        {
            ShelteredAPI.Interactions.InteractionRegistry.Register(targetType, interactionName, interactionType);
        }
    }
}
