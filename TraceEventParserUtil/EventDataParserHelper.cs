using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using PropertyBagType = System.Collections.Specialized.OrderedDictionary;

namespace TraceEventParserUtil
{
    internal class EventDataTypeUtil
    {
        static MethodInfo GetMethodInfo<T>(Expression<Func<T>> methodCall)
        {
            var methodCallExpression = (MethodCallExpression)methodCall.Body;
            return methodCallExpression.Method;
        }
        internal static ReadOnlyDictionary<Type, MethodInfo> ConversionMethods;

        internal static byte ToByte(byte[] data, int offset)
        {
            return data[offset];
        }

        internal static sbyte ToSByte(byte[] data, int offset)
        {
            return (sbyte)data[offset];
        }

        internal static IntPtr ToIntPtr(byte[] data, int offset)
        {
            if (IntPtr.Size == Marshal.SizeOf(typeof(int)))
            {
                return new IntPtr(BitConverter.ToInt32(data, offset));
            }
            else
            {
                return new IntPtr(BitConverter.ToInt64(data, offset));
            }
        }

        internal static UIntPtr ToUIntPtr(byte[] data, int offset)
        {
            if (UIntPtr.Size == Marshal.SizeOf(typeof(uint)))
            {
                return new UIntPtr(BitConverter.ToUInt32(data, offset));
            }
            else
            {
                return new UIntPtr(BitConverter.ToUInt64(data, offset));
            }
        }
        static EventDataTypeUtil()
        {
            var converterType = typeof(BitConverter);
            var bitConverterSupportedTypes = new[]
            {
                typeof (Int16),
                typeof (UInt16),
                typeof (Int32),
                typeof (UInt32),
                typeof (Int64),
                typeof (UInt64),
            };
            var conversionMethods = bitConverterSupportedTypes.ToDictionary(type => type, type => converterType.GetMethod($"To{type.Name}"));
            conversionMethods[typeof(byte)] = GetMethodInfo(() => ToByte(null, 0));
            conversionMethods[typeof(sbyte)] = GetMethodInfo(() => ToSByte(null, 0));
            conversionMethods[typeof(IntPtr)] = GetMethodInfo(() => ToIntPtr(null, 0));
            conversionMethods[typeof(UIntPtr)] = GetMethodInfo(() => ToUIntPtr(null, 0));

            ConversionMethods = new ReadOnlyDictionary<Type, MethodInfo>(conversionMethods);
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EventDataMemberAttribute : Attribute
    {
        public int Count { get; }
        public bool IsVariableLength { get; }
        public string LengthPropertyName { get; }

        public EventDataMemberAttribute() : this(0)
        {
        }

        public EventDataMemberAttribute(int count)
        {
            this.Count = count;
            this.IsVariableLength = false;
            this.LengthPropertyName = null;
        }

        public EventDataMemberAttribute(string lengthPropertyName)
        {
            this.Count = 0;
            this.IsVariableLength = true;
            this.LengthPropertyName = lengthPropertyName;
        }
    }

    public static class EventDataParserHelper
    {
        public static Delegate CreateInitializer(Type eventDataHostType, bool storeToPropertyBag)
        {
            var properties = eventDataHostType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var propertyBagType = typeof (PropertyBagType);
            var instanceParameter = storeToPropertyBag 
                ? Expression.Parameter(propertyBagType, "propertyBag")
                : Expression.Parameter(eventDataHostType, "instance");
            var eventDataParameter = Expression.Parameter(typeof(byte[]), "eventData");
            var offsetParameter = Expression.Parameter(typeof(int), "length");
            var lengthParameter = Expression.Parameter(typeof(int), "offsetArg");
            var bodyExpressions = new List<Expression>();
            var variables = new List<ParameterExpression>();
            var assignedPropertyValues = new Dictionary<string, Expression>();

            var offsetVariable = Expression.Variable(typeof(int), "offset"); variables.Add(offsetVariable);
            bodyExpressions.Add(Expression.Assign(offsetVariable, offsetParameter));

            var returnTarget = Expression.Label(typeof(int));

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<EventDataMemberAttribute>();
                if (attribute == null) continue;

                var type = property.PropertyType;
                Expression propertyValueExpression = null;
                Expression offsetUpdateExpression = null;
                MethodInfo conversionMethod;
                if (EventDataTypeUtil.ConversionMethods.TryGetValue(type, out conversionMethod))
                {
                    var callExpression = Expression.Call(conversionMethod, eventDataParameter, offsetVariable);
                    var propertyValueVariable = Expression.Variable(type);
                    variables.Add(propertyValueVariable);
                    bodyExpressions.Add(Expression.Assign(propertyValueVariable, callExpression));
                    propertyValueExpression = propertyValueVariable;
                    offsetUpdateExpression = Expression.AddAssign(offsetVariable, Expression.Constant(Marshal.SizeOf(type)));

                    // Store property value variable to calculate length of variable length fields.
                    assignedPropertyValues[property.Name] = Expression.Convert(propertyValueVariable, typeof(int));
                }
                else if (type.GetInterfaces().Any(@interface => @interface == typeof(IEventDataType)))
                {
                    var constructor = type.GetConstructor(new[] { typeof(byte[]), typeof(int), typeof(int) });
                    if (constructor == null)
                    {
                        throw new Exception($"Could not find a constructor. type = {type.Name}");
                    }
                    // var variable = new EventDataType(eventData, offset, length - offset);
                    // offset += ((IEventDataType)variable.Length);
                    var lengthExpression = Expression.Subtract(lengthParameter, offsetVariable);
                    var variable = Expression.Variable(type);
                    variables.Add(variable);
                    bodyExpressions.Add(Expression.Assign(variable, Expression.New(constructor, eventDataParameter, offsetVariable, lengthExpression)));
                    var lengthProperty = Expression.Property(variable, typeof (IEventDataType).GetProperty("Length"));
                    offsetUpdateExpression = Expression.AddAssign(offsetVariable, lengthProperty);
                    propertyValueExpression = variable;
                }
                else if (type.IsArray && EventDataTypeUtil.ConversionMethods.TryGetValue(type.GetElementType(), out conversionMethod))
                {
                    Expression countExpression;
                    if (attribute.IsVariableLength)
                    {
                        Expression lengthPropertyVariable;
                        if (!assignedPropertyValues.TryGetValue(attribute.LengthPropertyName, out lengthPropertyVariable)) throw new Exception($"The specified length field \"{attribute.LengthPropertyName}\" must be defined prior to a variable length field \"{property.Name}\".");

                        countExpression = lengthPropertyVariable;
                    }
                    else
                    {
                        if (attribute.Count < 0) throw new Exception($"Array count must not be negative. ${attribute.Count} is specified.");
                        countExpression = Expression.Constant(attribute.Count);
                    }

                    // Construct an array.
                    var elementType = type.GetElementType();
                    var elementSize = Marshal.SizeOf(elementType);
                    var arrayVariable = Expression.Variable(type);
                    variables.Add(arrayVariable);

                    bodyExpressions.Add(Expression.Assign(arrayVariable, Expression.NewArrayBounds(elementType, countExpression)));

                    // Initialize elements of the array.
                    var indexVariable = Expression.Variable(typeof(int));
                    variables.Add(indexVariable);

                    var breakLabel = Expression.Label();
                    // for(var index = 0; index < count; index++) 
                    // {
                    //    array[index] = converter(eventData, offset)
                    //    offset += elementSize;
                    // }
                    bodyExpressions.Add(Expression.Assign(indexVariable, Expression.Constant(0, typeof(int))));
                    var initializationLoopBody =
                        Expression.IfThenElse(
                            Expression.LessThan(indexVariable, countExpression),
                            Expression.Block(
                                Expression.Assign(
                                    Expression.ArrayAccess(arrayVariable, indexVariable),
                                    Expression.Call(conversionMethod, eventDataParameter, offsetVariable)),
                                //Expression.Call(typeof(System.Diagnostics.Debug).GetMethod("WriteLine", new [] {typeof(object)}), Expression.Convert(indexVariable, typeof(object))),
                                Expression.AddAssign(offsetVariable, Expression.Constant(elementSize)),
                                Expression.Assign(indexVariable, Expression.Increment(indexVariable))),
                            Expression.Break(breakLabel));
                    bodyExpressions.Add(Expression.Loop(initializationLoopBody, breakLabel));
                    propertyValueExpression = arrayVariable;
                }

                
                // this.property = propertyValue;
                //  or
                // propertyBag.Add(propertyName, propertyValue);
                if (storeToPropertyBag )
                {
                    if (propertyValueExpression.Type.IsValueType)
                    {
                        propertyValueExpression = Expression.Convert(propertyValueExpression, typeof (object));
                    }

                    var addMethod = propertyBagType.GetMethod("Add", new[] {typeof (string), typeof (object)});
                    bodyExpressions.Add(Expression.Call(instanceParameter, addMethod, Expression.Constant(property.Name), propertyValueExpression));
                }
                else
                {
                    bodyExpressions.Add(Expression.Assign(Expression.Property(instanceParameter, property), propertyValueExpression));
                }

                if (offsetUpdateExpression != null)
                {
                    bodyExpressions.Add(offsetUpdateExpression);
                }
            }

            // return (offset - offsetParameter);
            bodyExpressions.Add(Expression.Label(returnTarget, Expression.Subtract(offsetVariable, offsetParameter)));

            // Compile the expression tree.
            var delegateType = GetInitializerType(eventDataHostType, storeToPropertyBag);
            var lambda = Expression.Lambda(
                delegateType,
                Expression.Block(variables, bodyExpressions),
                instanceParameter, eventDataParameter, offsetParameter, lengthParameter);

            return lambda.Compile();
        }
        public static Type GetInitializerType(Type eventDataHostType, bool storeToPropertyBag)
        {
            var storageType = storeToPropertyBag ? typeof (PropertyBagType) : eventDataHostType;
            return typeof (Func<,,,,>).MakeGenericType(new[] { storageType, typeof (byte[]), typeof (int), typeof (int), typeof (int)});
        }

        public static Func<TEventDataHost, byte[], int, int, int> CreateInitializer<TEventDataHost>()
        {
            return (Func<TEventDataHost, byte[], int, int, int>) CreateInitializer(typeof (TEventDataHost), false);
        }
    }

    
}
