using System;
using System.Collections.Generic;
using System.Reflection;

namespace TickZoom.Api
{
    public class MetaType
    {
        private Type type;
        private int typeCode;
        private Dictionary<int,FieldInfo> members = new Dictionary<int, FieldInfo>();

        public MetaType(Type type, int typeCode)
        {
            this.type = type;
            this.typeCode = typeCode;
        }

        public Dictionary<int, FieldInfo> Members
        {
            get { return members; }
        }

        public Type Type
        {
            get { return type; }
        }

        public int TypeCode
        {
            get { return typeCode; }
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