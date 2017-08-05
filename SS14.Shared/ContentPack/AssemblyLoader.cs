﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SS14.Shared.Interfaces.Reflection;
using SS14.Shared.IoC;
using SS14.Shared.Log;

namespace SS14.Shared.ContentPack
{
    /// <summary>
    ///     Class for managing the loading of assemblies into the engine.
    /// </summary>
    public static class AssemblyLoader
    {
        /// <summary>
        ///     Run levels of the Content entry point.
        /// </summary>
        public enum RunLevel
        {
            Error = 0,
            Init = 1
        }

        static AssemblyLoader()
        {
            // Make it attempt to use already loaded assemblies,
            // as .NET doesn't do it automatically.
            // (It attempts to load dependencies from disk next to executable only)
            AppDomain.CurrentDomain.AssemblyResolve += ResolveMissingAssembly;
        }

        private static Assembly ResolveMissingAssembly(object sender, ResolveEventArgs args)
        {
            foreach (var mod in _mods)
            {
                if (mod.GameAssembly.FullName == args.Name)
                {
                    return mod.GameAssembly;
                }
            }
            return null;
        }

        /// <summary>
        ///     Loaded assemblies.
        /// </summary>
        private static readonly List<ModInfo> _mods = new List<ModInfo>();

        /// <summary>
        ///     Gets an assembly by name from the given AppDomain.
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Assembly GetAssemblyByName(this AppDomain domain, string name)
        {
            return domain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == name);
        }

        /// <summary>
        ///     Loads and assembly into the current AppDomain.
        /// </summary>
        /// <typeparam name="T">The type of the entry point to search for.</typeparam>
        /// <param name="assembly">Byte array of the assembly.</param>
        /// <param name="symbols">Optional byte array of the debug symbols.</param>
        public static void LoadGameAssembly<T>(byte[] assembly, byte[] symbols = null)
            where T : GameShared
        {
            AssemblyTypeChecker.DisableTypeCheck = false;
            AssemblyTypeChecker.DumpTypes = true;
            if (!AssemblyTypeChecker.CheckAssembly(assembly))
                return;

            var mod = new ModInfo();

            mod.GameAssembly = symbols != null
                ? AppDomain.CurrentDomain.Load(assembly, symbols)
                : AppDomain.CurrentDomain.Load(assembly);

            IoCManager.Resolve<IReflectionManager>().LoadAssemblies(mod.GameAssembly);

            var entryPoints = mod.GameAssembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t)).ToArray();

            if (entryPoints.Length == 0)
                Logger.Warning($"[RES] Assembly has no entry points: {mod.GameAssembly.FullName}");

            foreach (var entryPoint in entryPoints)
            {
                mod.EntryPoints.Add(Activator.CreateInstance(entryPoint) as T);
            }

            _mods.Add(mod);
        }

        /// <summary>
        ///     Broadcasts a run level change to all loaded entry point.
        /// </summary>
        /// <param name="level">New level</param>
        public static void BroadcastRunLevel(RunLevel level)
        {
            foreach (var mod in _mods)
            {
                foreach (var entry in mod.EntryPoints)
                {
                    switch (level)
                    {
                        case RunLevel.Init:
                            entry.Init();
                            break;
                        default:
                            Logger.Error($"[RES] Unknown RunLevel: {level}");
                            break;
                    }
                }
            }
        }

        /// <summary>
        ///     Holds info about a loaded assembly.
        /// </summary>
        private class ModInfo
        {
            public ModInfo()
            {
                EntryPoints = new List<GameShared>();
            }

            public Assembly GameAssembly { get; set; }
            public List<GameShared> EntryPoints { get; }
        }
    }
}
