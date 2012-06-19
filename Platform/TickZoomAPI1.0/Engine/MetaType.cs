using System;
using System.Collections.Generic;
using System.Reflection;

namespace TickZoom.Api
{
    public class MetaType
    {
        private Type type;
        private Dictionary<int,FieldInfo> members = new Dictionary<int, FieldInfo>();

        public MetaType(Type type)
        {
            this.type = type;
        }

        public Dictionary<int, FieldInfo> Members
        {
            get { return members; }
        }

        public Type Type
        {
            get { return type; }
        }

        public void Generate()
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var attributes = field.GetCustomAttributes(false);
                foreach (var attribute in attributes)
                {
                    var member = attribute as SerializeMemberAttribute;
                    if (member != null)
                    {
                        Members.Add(member.Id,field);
                    }
                }
            }
        }
    }
}