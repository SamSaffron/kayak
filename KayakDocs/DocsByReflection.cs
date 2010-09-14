﻿//Except where stated all code and programs in this project are the copyright of Jim Blackler, 2008.
//jimblackler@gmail.com
//
//This is free software. Libraries and programs are distributed under the terms of the GNU Lesser
//General Public License. Please see the files COPYING and COPYING.LESSER.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace JimBlackler.DocsByReflection
{
    /// <summary>
    /// Utility class to provide documentation for various types where available with the assembly
    /// </summary>
    public class DocsByReflection
    {
        /// <summary>
        /// Provides the documentation comments for a specific member
        /// </summary>
        /// <param name="memberInfo">The MemberInfo (reflection data) or the member to find documentation for</param>
        /// <returns>The XML fragment describing the member</returns>
        public static XmlElement XMLFromMember(MemberInfo memberInfo)
        {
            if (memberInfo is MethodInfo || memberInfo is ConstructorInfo)
            {
                var methodInfo = memberInfo as MethodInfo;
                var constructorInfo = memberInfo as ConstructorInfo;

                var parameters = methodInfo != null ? methodInfo.GetParameters() : constructorInfo.GetParameters();

                // Calculate the parameter string as this is in the member name in the XML
                string parametersString = "";
                foreach (ParameterInfo parameterInfo in parameters)
                {
                    if (parametersString.Length > 0)
                    {
                        parametersString += ",";
                    }

                    var parameterName = parameterInfo.ParameterType.FullName;

                    if (parameterInfo.ParameterType.IsGenericType)
                    {
                        var fullName = parameterInfo.ParameterType.FullName;
                        parameterName = fullName.Substring(0, fullName.IndexOf('`'))  + "{";

                        var genericArgs = parameterInfo.ParameterType.GetGenericArguments();

                        bool first = true;
                        foreach (var arg in genericArgs)
                        {
                            if (!first)
                                parameterName += ",";

                            parameterName += arg.FullName;
                            first = false;
                        }

                        parameterName += "}";
                    }

                    parametersString += parameterName;
                }

                var name = methodInfo != null ? methodInfo.Name : "#ctor";

                if (parametersString.Length > 0)
                    name += "(" + parametersString + ")";


                return XMLFromName(memberInfo.DeclaringType, 'M', name);
            }

            // First character [0] of member type is prefix character in the name in the XML
            return XMLFromName(memberInfo.DeclaringType, memberInfo.MemberType.ToString()[0], memberInfo.Name);
        }

        /// <summary>
        /// Provides the documentation comments for a specific type
        /// </summary>
        /// <param name="type">Type to find the documentation for</param>
        /// <returns>The XML fragment that describes the type</returns>
        public static XmlElement XMLFromType(Type type)
        {
            // Prefix in type names is T
            return XMLFromName(type, 'T', "");
        }

        /// <summary>
        /// Obtains the XML Element that describes a reflection element by searching the 
        /// members for a member that has a name that describes the element.
        /// </summary>
        /// <param name="type">The type or parent type, used to fetch the assembly</param>
        /// <param name="prefix">The prefix as seen in the name attribute in the documentation XML</param>
        /// <param name="name">Where relevant, the full name qualifier for the element</param>
        /// <returns>The member that has a name that describes the specified reflection element</returns>
        private static XmlElement XMLFromName(Type type, char prefix, string name)
        {
            string fullName;

            if (String.IsNullOrEmpty(name))
            {
                fullName = prefix + ":" + type.FullName;
            }
            else
            {
                fullName = prefix + ":" + type.FullName + "." + name;
            }

            XmlDocument xmlDocument = XMLFromAssembly(type.Assembly);

            XmlElement matchedElement = null;

            foreach (XmlElement xmlElement in xmlDocument["doc"]["members"])
            {
                if (xmlElement.Attributes["name"].Value.Equals(fullName))
                {
                    matchedElement = xmlElement;
                    break;
                }
            }

            return matchedElement;
        }

        /// <summary>
        /// A cache used to remember Xml documentation for assemblies
        /// </summary>
        static Dictionary<Assembly, XmlDocument> cache = new Dictionary<Assembly, XmlDocument>();

        /// <summary>
        /// A cache used to store failure exceptions for assembly lookups
        /// </summary>
        static Dictionary<Assembly, Exception> failCache = new Dictionary<Assembly, Exception>();

        /// <summary>
        /// Obtains the documentation file for the specified assembly
        /// </summary>
        /// <param name="assembly">The assembly to find the XML document for</param>
        /// <returns>The XML document</returns>
        /// <remarks>This version uses a cache to preserve the assemblies, so that 
        /// the XML file is not loaded and parsed on every single lookup</remarks>
        public static XmlDocument XMLFromAssembly(Assembly assembly)
        {
            if (failCache.ContainsKey(assembly))
            {
                throw failCache[assembly];
            }

            try
            {

                if (!cache.ContainsKey(assembly))
                {
                    // load the docuemnt into the cache
                    cache[assembly] = XMLFromAssemblyNonCached(assembly);
                }

                return cache[assembly];
            }
            catch (Exception exception)
            {
                failCache[assembly] = exception;
                throw exception;
            }
        }

        /// <summary>
        /// Loads and parses the documentation file for the specified assembly
        /// </summary>
        /// <param name="assembly">The assembly to find the XML document for</param>
        /// <returns>The XML document</returns>
        private static XmlDocument XMLFromAssemblyNonCached(Assembly assembly)
        {
            string assemblyFilename = assembly.CodeBase;

            const string prefix = "file:///";

            if (assemblyFilename.StartsWith(prefix))
            {
                StreamReader streamReader;

                try
                {
                    streamReader = new StreamReader(Path.ChangeExtension(assemblyFilename.Substring(prefix.Length), ".xml"));
                }
                catch (FileNotFoundException exception)
                {
                    throw new DocsByReflectionException("XML documentation not present (make sure it is turned on in project properties when building)", exception);
                }

                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(streamReader);
                return xmlDocument;
            }
            else
            {
                throw new DocsByReflectionException("Could not ascertain assembly filename", null);
            }
        }
    }


    /// <summary>
    /// An exception thrown by the DocsByReflection library
    /// </summary>
    [Serializable]
    class DocsByReflectionException : Exception
    {
        /// <summary>
        /// Initializes a new exception instance with the specified
        /// error message and a reference to the inner exception that is the cause of
        /// this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or null if none.</param>
        public DocsByReflectionException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}