using System;
using ModAPI.Core;

namespace ModAPI.Core
{
    /// <summary>
    /// Generic base class for mods using the Spine settings framework.
    /// Provides a strongly-typed Config property and automatic initialization.
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

            // If base.Initialize found inline settings on 'this' but strict T is requested:
            if (base.Config != null && !(base.Config is T))
            {
                 // Check if T IS the mod class (e.g. ModManagerBase<MyMod>)
                 if (typeof(T).IsAssignableFrom(GetType()))
                 {
                     // This is fine, Config is 'this', and 'this' is 'T'.
                 }
                 else
                 {
                     // User mixed patterns. We force T.
                     // Note: This effectively disables the inline settings found on 'this'.
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
