using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;

namespace TraceEventParserGenerator
{
    public class EntityName : IEquatable<EntityName>
    {
        private readonly List<string> parts;

        public IReadOnlyList<string> Parts => this.parts;

        private static string NormalizePart(string part)
        {
            return new string(part.Select(char.ToLower).Where(char.IsLetterOrDigit).ToArray());
        }

        public string this[int index]
        {
            get { return this.parts[index]; }
            set { this.parts[index] = NormalizePart(value); }
        }

        public void AppendPart(string part)
        {
            this.parts.Add(NormalizePart(part));
        }

        public EntityName(IEnumerable<string> parts)
        {
            this.parts = parts.Select(NormalizePart).ToList();
        }

        #region Equality
        public bool Equals(EntityName other)
        {
            return (object) other != null &&
                   ((this.Parts == other.Parts) || (this.Parts != null && other.Parts != null && this.Parts.SequenceEqual(other.Parts)));
        }

        
        public override bool Equals(object other)
        {
            var otherEntity = other as EntityName;
            return otherEntity != null && this.Equals(otherEntity);
        }

        public override int GetHashCode()
        {
            return this.Parts?.Select(part => part.GetHashCode()).Aggregate((l, r) => l ^ r) ?? 0;
        }

        public static bool operator ==(EntityName lhs, EntityName rhs)
        {
            return object.ReferenceEquals(lhs, rhs) ||
                   ((object) lhs != null && (object) rhs != null && lhs.Equals(rhs));
        }

        public static bool operator !=(EntityName lhs, EntityName rhs)
        {
            return !(lhs == rhs);
        }
        #endregion
    }

    public interface INamingConvension
    {
        string Format(EntityName entityName);
        EntityName Parse(string name);
    }

    public class SnakeCaseNamingConvension : INamingConvension
    {
        public string Format(EntityName entityName)
        {
            return string.Join("_", entityName.Parts);
        }

        public EntityName Parse(string name)
        {
            return new EntityName(name.Split('_').Where(s => s.Length > 0));
        }
    }
    public class CamelCaseNamingConvension : INamingConvension
    {
        private readonly bool upperCamelCase;

        public CamelCaseNamingConvension(bool upperCamelCase)
        {
            this.upperCamelCase = upperCamelCase;
        }

        public string Format(EntityName entityName)
        {
            var builder = new StringBuilder();
            foreach (var part in entityName.Parts)
            {
                if (builder.Length == 0 && !this.upperCamelCase)
                {
                    builder.Append(part);
                }
                else
                {
                    builder.Append(char.ToUpper(part.First()));
                    foreach (var c in part.Skip(1))
                    {
                        builder.Append(c);
                    }
                }
            }

            return builder.ToString();
        }

        private IEnumerable<string> InnerParse(string name)
        {
            var builder = new StringBuilder();
            foreach (var c in name)
            {
                if (char.IsUpper(c) && builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }
                builder.Append(char.ToLower(c));
            }
            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        public EntityName Parse(string name)
        {
            return new EntityName(this.InnerParse(name));
        }
    }

}
