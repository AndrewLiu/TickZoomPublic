using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

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
            var attribs = type.GetCustomAttributes(typeof (SerializeContractAttribute), false);
            if( attribs.Length == 0)
            {
                throw new SerializationException(type.FullName + " does not have " + typeof(SerializeContractAttribute).Name + " applied.");
            }
            var count = 0;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var attributes = field.GetCustomAttributes(typeof (SerializeMemberAttribute),false);
                foreach (var attribute in attributes)
                {
                    var member = attribute as SerializeMemberAttribute;
                    if (member != null)
                    {
                        Members.Add(member.Id,field);
                        count++;
                    }
                }
            }
            if( count == 0)
            {
                throw new SerializationException(type.FullName + " doesn't have any field with " + typeof(SerializeMemberAttribute).Name + " applied.");
            }
        }
    }
}