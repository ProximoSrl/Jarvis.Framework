﻿using Newtonsoft.Json;
using System;

namespace Jarvis.Framework.Shared.Claims
{
    public sealed class Claim : IEquatable<Claim>
    {
        public bool Equals(Claim other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id) && string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Claim)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(Claim left, Claim right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Claim left, Claim right)
        {
            return !Equals(left, right);
        }

        public string Id { get; private set; }

        public string Value { get; private set; }

        [JsonConstructor]
        public Claim(string id, string value)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            Id = id;
            Value = value;
        }

        /// <summary>
        /// Overload for default value of "value" parameter
        /// </summary>
        /// <param name="id"></param>
        public Claim(string id) : this(id, "True")
        {
        }

        static public Claim For(string id, string value)
        {
            return new Claim(id, value);
        }
    }
}
