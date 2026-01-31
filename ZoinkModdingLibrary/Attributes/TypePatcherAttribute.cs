using System;
using ZoinkModdingLibrary.Utils;

namespace ZoinkModdingLibrary.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TypePatcherAttribute : Attribute
    {
        private string targetAssemblyName;
        private string targetTypeName;
        private Type? targetType;

        public string TargetAssemblyName => targetAssemblyName;
        public string TargetTypeName => targetTypeName;
        public bool IsCertain { get; }
        public Type? TargetType
        {
            get
            {
                if (targetType == null)
                {
                    targetType = AssemblyOperations.FindTypeInAssemblies(targetAssemblyName, targetTypeName);
                }
                return targetType;
            }
        }

        public TypePatcherAttribute(string targetAssemblyName, string targetTypeName)
        {
            this.targetAssemblyName = targetAssemblyName;
            this.targetTypeName = targetTypeName;
            IsCertain = false;
        }

        public TypePatcherAttribute(Type targetType)
        {
            this.targetType = targetType;
            this.targetAssemblyName = targetType.Assembly.FullName;
            this.targetTypeName = targetType.Name;
            IsCertain = true;
        }
    }
}
