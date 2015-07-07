using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace IthacaKeyServer.Config
{
    public enum ConvarType
    {
        Integer,    /* int */
        String,     /* string */
        Boolean,    /* bool */
        List        /* string[] */
    }

    public class Convar
    {
        private string m_name;

        private ConvarType m_type;
        private object m_value;

        public string Name
        {
            get { return m_name; }
        }

        public Convar(string name, string value)
        {
            this.m_name = name;
            SetTypeAndValue(value, out m_value, out m_type);
        }

        public Convar(string name, string value, ConvarType type)
        {
            this.m_name = name;
            this.m_type = type;
            SetValueFromType(value, m_type, out m_value);
        }

        private void SetValueFromType(string sourceStr, ConvarType type, out object value)
        {
            switch (type)
            {
                case ConvarType.Integer:
                    value = int.Parse(sourceStr);
                    break;
                case ConvarType.Boolean:
                    value = bool.Parse(sourceStr);
                    break;
                case ConvarType.List:
                    value = sourceStr.Split(';');
                    break;
                case ConvarType.String:
                    value = sourceStr;
                    break;
            }

            value = null;
        }

        private bool SetTypeAndValue(string sourceStr, out object value, out ConvarType type)
        {
            // The only thing this function can do is check for integer and boolean types.
            // Lists and strings could be interpreted as each other.
            int retInt = 0;
            bool retBool = false;
            string retString = string.Empty;
            string[] retArray = null;


            if (int.TryParse(sourceStr, out retInt))
            {
                type = ConvarType.Integer;
                value = retInt;
                return true;
            }
            else if (bool.TryParse(sourceStr, out retBool))
            {
                type = ConvarType.Boolean;
                value = retBool;
                return true;
            }
            else
            {
                retArray = sourceStr.Split(';');
                if (retArray.Length > 1)
                {
                    type = ConvarType.List;
                    value = retArray;
                    return true;
                }
                else
                {
                    type = ConvarType.String;
                    value = sourceStr;
                    return true;
                }
            }
        }

        public void SetValue(string value, ConvarType type)
        {
            this.m_type = type;
            SetValueFromType(value, m_type, out m_value);
        }

        public void SetValue(string value)
        {
            bool success = SetTypeAndValue(value, out m_value, out m_type);
            if (success) return;
            else throw new Exception("Error occurred while setting value.");
        }

        public object GetValue()
        {
            return m_value;
        }
    }

    public class Configuration
    {
        // Gets the value of the first convar with the name s
        public object this[string s]
        {
            get
            {
                int index = 0;
                for (int i = 0; i < m_convars.Count; i++)
                {
                    if (m_convars[i].Name == s)
                    {
                        index = i;
                        break;
                    }
                }

                return m_convars[index].GetValue();
            }
        }

        private List<Convar> m_convars;

        public Configuration()
        {
            this.m_convars = new List<Convar>();
        }


        public void ParseConfigXml(Stream s)
        {
            // Assumes the s.Position is 0

            XmlDocument doc = new XmlDocument();
            doc.Load(s);

            XmlNode root = doc.FirstChild;
            if (root.NodeType == XmlNodeType.XmlDeclaration)
            {
                root = root.NextSibling;
            }

            foreach (XmlNode configOption in root.ChildNodes)
            {
                if (configOption.NodeType == XmlNodeType.Comment)
                    continue;

                // First, check if there is an attribute called type:
                ConvarType type = default(ConvarType);
                bool typeSet = false;
                try
                {
                    XmlAttribute typeAttribute = configOption.Attributes["type"];
                    string typeAttributeVal = typeAttribute.Value;
                    if (typeAttributeVal == "int")
                    {
                        type = ConvarType.Integer;
                        typeSet = true;
                    }
                    else if (typeAttributeVal == "string")
                    {
                        type = ConvarType.String;
                        typeSet = true;
                    }
                    else if (typeAttributeVal == "bool")
                    {
                        type = ConvarType.Boolean;
                        typeSet = true;
                    }
                    else if (typeAttributeVal == "list")
                    {
                        type = ConvarType.List;
                        typeSet = true;
                    }
                }
                catch
                {
                    // This means that there is no attribute named type.
                    // The Convar constructor will try to infer it later.
                }

                string name = configOption.Name;
                string value = string.Empty;
                if (configOption.FirstChild.NodeType == XmlNodeType.Text)
                    value = configOption.FirstChild.Value;
                else
                {
                    throw new XmlException("Poorly formed config.xml.");
                }

                // Add the convar
                Convar c = null;
                if (typeSet)
                    c = new Convar(name, value, type);
                else
                    c = new Convar(name, value);
                m_convars.Add(c);
            }
        }

        public object GetConfigValue(string name)
        {
            int index = 0;
            for (int i = 0; i < m_convars.Count; i++)
            {
                if (m_convars[i].Name == name)
                {
                    index = i;
                    break;
                }
            }

            return m_convars[index].GetValue();
        }

        public void SetConfigValue(string name, string value, ConvarType type)
        {
            for (int i = 0; i < m_convars.Count; i++)
            {
                if (m_convars[i].Name == name)
                {
                    m_convars[i].SetValue(value, type);
                    break;
                }
            }
        }
        public void SetConfigValue(string name, string value)
        {
            for (int i = 0; i < m_convars.Count; i++)
            {
                if (m_convars[i].Name == name)
                {
                    m_convars[i].SetValue(value);
                    break;
                }
            }
        }
    }
}
