using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ConsoleCommands
{
    public class UpgradeCommand : ICommand
    {
        public string Name => "upgrade";
        public string Description => "Upgrades a system to its maximum level. Usage: upgrade <system_name>";

        public string Execute(string[] args)
        {
            if (ObjectManager.Instance == null)
            {
                return "ObjectManager not found.";
            }

            if (args.Length == 0)
            {
                return ListUpgradableSystems();
            }

            string systemName = args[0];
            List<Obj_Base> allObjects = ObjectManager.Instance.GetAllObjects();
            Obj_Base targetObject = allObjects.Find(obj => obj.name.Equals(systemName, System.StringComparison.OrdinalIgnoreCase));

            if (targetObject == null)
            {
                return $"System '{systemName}' not found.";
            }

            UpgradeObject upgradeComponent = targetObject.GetComponent<UpgradeObject>();
            if (upgradeComponent == null)
            {
                return $"System '{systemName}' is not upgradable.";
            }

            List<UpgradeObject.PathEnum> paths = upgradeComponent.GetPaths();
            foreach (UpgradeObject.PathEnum path in paths)
            {
                int maxLevel = upgradeComponent.GetMaxUpgradeLevel(path);
                upgradeComponent.Upgrade(path, maxLevel);
            }

            return $"Upgraded '{systemName}' to maximum level.";
        }

        private string ListUpgradableSystems()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Available systems to upgrade:");

            List<Obj_Base> allObjects = ObjectManager.Instance.GetAllObjects();
            foreach (Obj_Base obj in allObjects)
            {
                UpgradeObject upgradeComponent = obj.GetComponent<UpgradeObject>();
                if (upgradeComponent != null)
                {
                    sb.AppendLine($"- {obj.name}");
                    List<UpgradeObject.PathEnum> paths = upgradeComponent.GetPaths();
                    foreach (UpgradeObject.PathEnum path in paths)
                    {
                        int currentLevel = upgradeComponent.GetUpgradeLevel(path);
                        int maxLevel = upgradeComponent.GetMaxUpgradeLevel(path);
                        sb.AppendLine($"  - {path} ({currentLevel}/{maxLevel})");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
