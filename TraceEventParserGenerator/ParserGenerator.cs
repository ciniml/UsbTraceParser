using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using TraceEventParserGenerator.Manifest;

namespace TraceEventParserGenerator
{

    public class ParserGenerator
    {
        private const string ParserUtilNamespace = "TraceEventParserUtil";
        private static Dictionary<XmlQualifiedName, Type> eventDataTypes = new Dictionary<XmlQualifiedName, Type>()
        {
            {new XmlQualifiedName("Pointer", "win"), typeof(ulong) },
            {new XmlQualifiedName("Int8", "win"), typeof(sbyte) },
            {new XmlQualifiedName("UInt8", "win"), typeof(byte) },
            {new XmlQualifiedName("Int16", "win"), typeof(short) },
            {new XmlQualifiedName("UInt16", "win"), typeof(ushort) },
            {new XmlQualifiedName("Int32", "win"), typeof(int) },
            {new XmlQualifiedName("UInt32", "win"), typeof(uint) },
            {new XmlQualifiedName("Int64", "win"), typeof(long) },
            {new XmlQualifiedName("UInt64", "win"), typeof(ulong) },
            {new XmlQualifiedName("Float", "win"), typeof(float) },
            {new XmlQualifiedName("Double", "win"), typeof(double) },
            {new XmlQualifiedName("Boolean", "win"), typeof(bool) },
            {new XmlQualifiedName("GUID", "win"), typeof(Guid) },
        };

        private Dictionary<string, string> structNames = new Dictionary<string, string>();
        private CodeDomProvider provider;
        private ICodeGenerator generator;

        private INamingConvension inputStructNamingConvension;
        private Func<EntityName, string> structNameConverter;

        private const string TraceNamespaceUri = "http://schemas.microsoft.com/win/2004/08/events/trace";
        private const string WindowsEventNamespaceUri = "http://manifests.microsoft.com/win/2004/08/windows/events";

        
        private void GenerateEventStructClass(StructDefinitionType structDefinition)
        {
            var name = structDefinition.name;
            var structTypeAttribute = structDefinition.AnyAttr.SingleOrDefault(attribute => attribute.NamespaceURI == TraceNamespaceUri && attribute.Name == "structType");
            if (structTypeAttribute != null)
            {
                name = structTypeAttribute.Value;
            }
            var entityName = this.inputStructNamingConvension.Parse(name);
            var structName = this.structNameConverter(entityName);

            var typeDeclaration = new CodeTypeDeclaration(structName)
            {
                IsClass = true,
                TypeAttributes = TypeAttributes.Sealed | TypeAttributes.Public,
            };

            // Inherit from the EventDataType<StructType>
            var baseType = new CodeTypeReference($"{ParserUtilNamespace}.EventDataType");
            baseType.TypeArguments.Add(new CodeTypeReference(structName));
            typeDeclaration.BaseTypes.Add(baseType);

            foreach (var data in structDefinition.data)
            {
                Type type;
                string typeName;
                var member = new CodeMemberProperty();
                
                if (eventDataTypes.TryGetValue(data.inType, out type))
                {
                    member.Type = new CodeTypeReference(type);
                }
                else if( this.structNames.TryGetValue(data.inType.Name, out typeName))
                {
                    member.Type = new CodeTypeReference(typeName);
                }
                else
                {
                    throw new Exception($"Data type \"{data.inType}\" was not found. ");
                }

                data.name
            }
        }
        public ParserGenerator()
        {
            
        }
    }
}
