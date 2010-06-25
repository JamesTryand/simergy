using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Xml;
using System.IO;

namespace Simbiosis
{


	// XML Syntax:
	// 
	// TYPE - type of cell at this location (MUST be first entry in the gene)
	// <type> [celltype] </type> OR...
	// <type> [DLLassemblyname].[celltype] </type>
	//				e.g. <type> plankton.volvox </type>
	//
	// SOCKET - defines which sock on my parent I'm attached to
	// <socket> [name of socket on parent] </socket>
	//				e.g. <socket> skt0 </socket>
	//
	// ORIENTATION - how I'm oriented relative to my parent's socket
	// <orientation> [list of 16 comma separated values representing a matrix] </orientation>
	//
	// WIRING
	// Input tags declare the next functional block input in sequence
	// Output tags declare the next functional block output in sequence
	// [string] is one of: constant, chemical, yin, yang
	// <input>
	//		<type> [string] </type>
	//		<addr> ## </addr>
	// </input>
	// <output>
	//		<type> [string] </type>
	//		<addr> ## </addr>
	// </output>




	/// <summary>
	/// One gene in the Genome tree.
	/// Each gene relates to one cell of a creature. Genes are arranged into a TREE structure, which defines
	/// how the various cells relate to each other.
	/// </summary>
	/// <remarks>
	/// The root gene is the HEART of the creature.
	/// Each subsequent gene defines a specific body Cell, composed of a number of frames and one or more meshes.
	/// The root frame on a child body part connects to a named frame on its parent.
	/// 
	/// 
	/// 
	/// </remarks>

	public class Gene
	{
		public string Type = null;							// type of cell ("stinger01", etc).
		public string Socket = null;						// name of socket (joint) on parent to which I'm attached
        
		public Matrix Orientation = Matrix.Identity;		// attachment position and orientation

		public Gene Sibling = null;							// siblings in the genome tree
		public Gene FirstChild = null;						// first child in the genome tree

		// Lists of Channel structs defining the wiring
		public List<Channel> Channels = new List<Channel>();



		/// <summary>
		/// Read this <gene></gene> and any nested genes too
		/// </summary>
		/// <param name="xml"></param>
		/// <param name="previous">previous SIBLING of the gene being created</param>
		/// <returns></returns>
		public static Gene RecursiveRead(XmlTextReader xml, Gene previous)
		{
			Gene gene = null;
			Gene elder = null;
			string tag = "";

			while (xml.Read())												// for each node...
			{
				// <tag>
				if (xml.NodeType == XmlNodeType.Element)
				{
					// Handle the tag
					switch (xml.Name)										// depending on tag...
					{
							// A child gene definition is nested inside this one, so create it recursively
						case "gene":
							// the first such gene is the child of this one
							if (gene.FirstChild==null)
							{
								gene.FirstChild = RecursiveRead(xml,null);
								elder = gene.FirstChild;
							}
							// subsequent genes are siblings of each other
							else
							{
								elder.Sibling = RecursiveRead(xml,null);
								elder = elder.Sibling;
							}
							break;

						// A channel
						case "channel":
							gene.Channels.Add(ReadChannel(xml));
							break;

							// anything else must be a standalone <tag>text</tag>, so wait for its text to arrive
						default:
							tag = xml.Name;
							break;					
					}


				}
				// text within tags
				else if (xml.NodeType == XmlNodeType.Text)
				{
					switch (tag)			// action depends on the tag this text is part of
					{
							// create the named gene as a sibling of the previous one
						case "type":										// <type> this gene's type </type>
							gene = new Gene();								// start a new gene
							gene.Type = xml.Value;							// store its type name
							break;
							// <attachment>
						case "socket":
							gene.Socket = xml.Value;
							break;
							// <position>
						case "orientation":
							gene.Orientation = ReadMatrix(xml.Value);
							break;
						
							// Other gene initialisers here

						default:
							throw new XmlException("unexpected gene tag type: "+tag);
					}
				}
				// closing tag
				else if (xml.NodeType == XmlNodeType.EndElement)
				{
					if (xml.Name == "gene")
					{
						return gene;										// return the gene I just created
					}
				}
			}

			throw new XmlException("missing </gene> tag");					// should always return before end of file
		}


		/// <summary>
		/// Recurse through a genome writing the genes to an XML file
		/// </summary>
		/// <param name="xml">The output stream</param>
		/// <param name="gene">The gene to write (and then descend into)</param>
		public static void RecursiveWrite(XmlWriter xml, Gene gene)
		{
			xml.WriteStartElement("gene");
			xml.WriteElementString("type", gene.Type);
			if (gene.Socket!=null)
				xml.WriteElementString("socket", gene.Socket);
			xml.WriteElementString("orientation", EncodeMatrix(gene.Orientation));

			// Write <channel> nodes
			foreach (Channel c in gene.Channels)
			{
				c.WriteXml(xml);
			}

			// If this gene has children, nest them inside
			if (gene.FirstChild != null)
				RecursiveWrite(xml, gene.FirstChild);

			// Close the </gene>
			xml.WriteEndElement();

			// If this gene has siblings, write them at the same level
			if (gene.Sibling != null)
				RecursiveWrite(xml, gene.Sibling);

		}

		/// <summary>
		/// Write a matrix as a single line string
		/// </summary>
		/// <param name="m"></param>
		/// <returns></returns>
		private static string EncodeMatrix(Matrix m)
		{
			return String.Format("{0},{1},{2},{3},  {4},{5},{6},{7},  {8},{9},{10},{11},  {12},{13},{14},{15}",
				m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
		}



		/// <summary>
		/// Default constr - completely blank gene
		/// </summary>
		public Gene()
		{
		}

		/// <summary>
		/// Construct a gene of a given type with default parameters 
		/// (e.g. for creating an initial genome for a new creature)
		/// </summary>
		public Gene(string type)
		{
			this.Type = type;
		}



		/// <summary>
		/// Return the filename of this gene's body Cell
		/// </summary>
		/// <returns>the filename MINUS the .X extension</returns>
		public string XFile()
		{
			return Type;
		}

		/// <summary>
		/// Read a string of 3 comma-separated values - yaw, pitch, roll - into a 4 x 4 transformation matrix
		/// </summary>
		/// <param name="csv"></param>
		/// <returns></returns>
		public static Matrix ReadYawPitchRoll(string csv)
		{
			Matrix mat = Matrix.Identity;
			int comma=0, start=0;
			float[] ypr = new float[3];

			for (int n=0; n<3; n++)
			{
				start = csv.IndexOf(",",comma);
				if (comma<0)
					throw new System.ArgumentException("Genome: error in yaw, pitch, roll ["+csv+"]");
				ypr[n] = System.Convert.ToSingle(csv.Substring(start,comma-start));
				start = comma+1;
			}
			return mat;
		}

		/// <summary>
		/// Read a string of 16 comma-separated values into a 4 x 4 transformation matrix
		/// </summary>
		/// <param name="csv"></param>
		/// <returns></returns>
		public static Matrix ReadMatrix(string csv)
		{
			Matrix mat = Matrix.Identity;
			string v = null;

			int comma=0, start=0;
			float[] matval = new float[16];

			try
			{
				for (int n=0; n<16; n++)
				{
					comma = csv.IndexOf(",",start);
					if (comma<0)
						v = csv.Substring(start);
					else
						v = csv.Substring(start,comma-start);
					matval[n] = System.Convert.ToSingle(v);
					start = comma+1;
				}
			}
			catch
			{
				throw new System.ArgumentException("Genome: error in matrix ["+csv+"]");
			}
			mat.M11 = matval[0];
			mat.M12 = matval[1];
			mat.M13 = matval[2];
			mat.M14 = matval[3];

			mat.M21 = matval[4];
			mat.M22 = matval[5];
			mat.M23 = matval[6];
			mat.M24 = matval[7];

			mat.M31 = matval[8];
			mat.M32 = matval[9];
			mat.M33 = matval[10];
			mat.M34 = matval[11];

			mat.M41 = matval[12];
			mat.M42 = matval[13];
			mat.M43 = matval[14];
			mat.M44 = matval[15];

			return mat;
		}

		/// <summary>
		/// Read a channel node (note: not all members of Channel are filled (e.g. mesh frames))
		/// </summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public static Channel ReadChannel(XmlTextReader xml)
		{
		    string tag = "";
		    Channel channel = new Channel();

		    while (xml.Read())												// for each node...
		    {
		        // <tag>
		        if (xml.NodeType == XmlNodeType.Element)
		        {
		            tag = xml.Name;
		        }
		        // text within tags
		        else if (xml.NodeType == XmlNodeType.Text)
		        {
					channel.ReadXml(xml, tag);
		        }
		        // closing tag
		        else if (xml.NodeType == XmlNodeType.EndElement)
		        {
		            if (xml.Name == "channel")
		            {
		                return channel;										// return the channel I just created
		            }
		        }
		    }

		    throw new XmlException("missing </channel> tag");				// should always return before end of file
			
		}


		/// <summary>
		/// Return a simple array of the channels as read from the genome.
		/// </summary>
		/// <returns></returns>
		public Channel[] GetChannels()
		{
			// The entries are in a list here for ease of adding, but Cells want them as exact-sized simple arrays
			return (Channel[])Channels.ToArray();
		}


	}




	/// <summary>
	/// The complete genetic makeup of a creature and all the operations that can be performed on it.
	/// Genomes are used to create creatures but also supply important information to other creatures' senses
	/// for use in recognition of predators, prey and mates, etc.
	/// Each Genome is a tree structure made from Genes. It can be persisted/created as an XML file.
	/// </summary>
	public class Genome
	{

		/// <summary> The root gene in the genome tree </summary>
		Gene root = null;
		public Gene Root { get { return root; } }

		/// <summary> unique ID (filename) </summary>
		private string name = "untitled";
		public string Name { get { return name; } set { name = value; } }



		/// <summary>
		/// Create a minimal genome for building a new creature.
		/// The genome consists of a single gene defining a CORE cell
		/// </summary>
		public Genome()
		{
			root = new Gene("Core");
		}

		/// <summary>
		/// Load a genome from the <genome></genome> element in an XML file
		/// (could be a whole xml file or a section of one defining a creature instance, whose xyz etc. are extracted
		/// elsewhere)
		/// </summary>
		/// <param name="stream"></param>
		public Genome(XmlTextReader xml)
		{
			try
			{
				Read(xml);
			}
			catch
			{
				throw new XmlException("Failed to read genome from xml stream");
			}
		}

		/// <summary>
		/// Load a genome from an XML file containing nothing BUT a genome
		/// </summary>
		/// <param name="filename"></param>
		public Genome(string filename)
		{
			XmlTextReader xml = null;

			name = filename;

			try
			{
				xml = new XmlTextReader(FileResource.Fsp("genomes", filename+".xml"));		// create an XmlTextReader for a file
				xml.WhitespaceHandling = WhitespaceHandling.None;				// ignore white space
				xml.MoveToContent();											// skip any headers and go inside the root node
			}
			catch
			{
				throw new FileNotFoundException("Couldn't find genome file ["+filename+"]");
			}
			try
			{
				Read(xml);														// read in the data
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.ToString());
				throw new XmlException("Failed to read genome from xml file: ["+filename+"]",e);
			}

//			DebugHierarchy(root,0);			// HACK: SHOW GENE HIERARCHY
		}

		/// <summary>
		/// Read genome from XML stream
		/// </summary>
		/// <param name="xml"></param>
		private void Read(XmlTextReader xml)
		{
			string tag = "";

			while (xml.Read())												// for each node in the file...
			{
				if (xml.NodeType == XmlNodeType.Element)					// <tag> rather than final </document>
				{
					switch (xml.Name)										// depending on tag...
					{
						case "gene":										// <gene> the ROOT gene </gene>
							root = Gene.RecursiveRead(xml,null);			// read recursively to support part hierarchy
							break;
							// Any other root level tags go here

							// anything else must be a data tag, so wait for its text to arrive
						default:
							tag = xml.Name;
							break;					
					}

				}
					// text within tags
				else if (xml.NodeType == XmlNodeType.Text)
				{
					switch (tag)
					{
							// data tags relating to the Genome as a whole (not a gene) go here
						default:
							throw new XmlException("unexpected tag type: "+tag);
					}
				}
			}
		}


		/// <summary>
		/// Print an entire genome's hierarchy to the debug output stream
		/// </summary>
		/// <param name="root">root of hierarchy</param>
		/// <param name="level">current depth into hierarchy (set this to 0)</param>
		/// <returns></returns>
		public static void DebugHierarchy(Gene root, int level)
		{
			// set up an indent level
			string indent = "";
			for (int i=0; i<level; i++)
				indent += "\t";

			Debug.WriteLine(indent+"*** GENE ["+root.Type+"] ***");
			Debug.WriteLine(indent+"    Socket: ["+root.Socket+"]");

			// continue walking the tree
			if (root.FirstChild!=null)
			{
				DebugHierarchy(root.FirstChild, level+1);
			}
			if (root.Sibling!=null)
			{
				DebugHierarchy(root.Sibling, level);
			}
		}

		/// <summary>
		/// Write this genome to an XML file. The filename is the same as the Genome.name member
		/// and the file is written to the /Genomes folder
		/// </summary>
		public void Write()
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Indent = true;
			settings.IndentChars = ("    ");
			using (XmlWriter writer = XmlWriter.Create(FileResource.Fsp("Genomes",this.name+".xml"), settings))
			{
				writer.WriteStartElement("genome");
				Gene.RecursiveWrite(writer, root);
				writer.WriteEndElement();
				writer.Flush();
			}
		}




		// TODO: ADD OTHER METHODS FOR CROSSING-OVER, RETURNING SENSORY INFORMATION, COMPARING PREDATORS WITH PREY ETC.






	}
}
