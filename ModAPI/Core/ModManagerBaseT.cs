using System;
using ModAPI.Core;

namespace ModAPI.Core
{
    /// <summary>
    /// Generic base class for mods using the Spine settings framework.
    /// Adds a strongly typed <c>Config</c> property to <see cref="ModManagerBase"/>.
    /// </summary>
    /// <typeparam name="T">The class containing your [ModSetting] fields.</typeparam>
    public abstract class ModManagerBase<T> : ModManagerBase where T : class, new()
    {
        /// <summary>
        /// The active settings configuration instance.
        /// </summary>
        public new T Config 
        { 
            get 
            { 
                 return base.Config as T; 
            } 
        }

        public override void Initialize(IPluginContext context)
        {
            base.Initialize(context);

            // base.Initialize may use this instance as the inline settings object.
            if (base.Config != null && !(base.Config is T))
            {
                 if (typeof(T).IsAssignableFrom(GetType()))
                 {
                     // The inline settings object already has type T.
                 }
                 else
                 {
                     // Replace the incompatible inline object with a new T instance below.
                     base.Config = null;
                 }
            }

            if (base.Config == null)
            {
                CreateSettings<T>();
            }
        }
    }
}
