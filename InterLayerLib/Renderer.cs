using System.Text;
using System.Data;
using System;

namespace InterLayerLib
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class Renderer
	{
		public DataTable dtr;
		//public Summary summaryForm;

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="summaryForm"></param>
		public Renderer(DataTable VRtableWeb)
		{
			dtr = VRtableWeb;
		}
		public abstract string DrawHeader(); //
		public abstract string DrawRows(); //
	}

	/// <summary>
	/// Prepare Verification Table Results (VTR) into string for HTML representation structure
	/// </summary>
	public class HTMLRenderer : Renderer
	{
		StringBuilder sb = new StringBuilder();	
		public const string endTableNodeSize = "</table>"; 
		
		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="summaryForm"></param>
		public HTMLRenderer(DataTable dtr) : base(dtr)
		{	
		}

		/// <summary>
		/// Set style for VTR
		/// </summary>
		public void SetStyle() 
		{
			sb.AppendLine("<html> <head> <meta charset=\"utf-8\"> <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"> <link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/4.4.1/css/bootstrap.min.css\"> <script src=\"https://ajax.googleapis.com/ajax/libs/jquery/3.5.1/jquery.min.js\"></script> <script src=\"https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.16.0/umd/popper.min.js\"></script> <script type=\"text/javascript\" src=\"/js/hon-dls-swipe-lib_min.js\"></script> </head><div class=\"container\"> <table class=\"table table-striped sortable\"></table></div></html>");
		}

		/// <summary>
		/// Rendering VTR header
		/// </summary>
		/// <returns>string in form of HTML structure with enrichment of table header</returns>
		public override string DrawHeader() 
		{
			StringBuilder temp = new StringBuilder();
			temp.AppendLine("<thead class=\"thead-dark\"><tr>");
			Console.WriteLine("dtr {0}", dtr == null);
			for (int i = 0; i < dtr.Columns.Count; i++)
			{
				temp.AppendLine("<th>");
				temp.AppendLine(dtr.Columns[i].ToString());
				temp.AppendLine("</th>");
			}			
			temp.AppendLine("</tr></thead>");
			sb.Insert(sb.ToString().IndexOf(endTableNodeSize), temp);
			return sb.ToString();
			
		}

		/// <summary>
		/// Rendering VTR rows
		/// </summary>
		/// <returns>string in form of HTML structure with enrichment of table rows</returns>
		public override string DrawRows()
		{
			StringBuilder temp = new StringBuilder();
			temp.AppendLine("<tbody>");
			for (int i = 0; i < dtr.Rows.Count-1; i++)
			{
				temp.AppendLine("<tr>");
				for (int j = 0; j < dtr.Rows[i].ItemArray.Length; j++)
				{
					temp.AppendLine("<td>");
					temp.AppendLine(dtr.Rows[i].ItemArray[j].ToString());
					temp.AppendLine("</td>");
				}
				temp.AppendLine("</tr>");
			}		
			temp.AppendLine("</tbody>");
			sb.Insert(sb.ToString().IndexOf(endTableNodeSize), temp);
			return sb.ToString();
		}
		
		public string ColoriseText() 
		{
			//TODO
			return "";
		}

		public string ColoriseCell()
		{
			//TODO
			return "";
		}
	}
}
