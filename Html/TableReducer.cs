using System;
using System.Collections.Generic;

using AngleSharp.Html.Dom;
using AngleSharp.Dom;

namespace HtmlToGmi.Html
{
	public class TableReducer
	{

		List<INode> nodes;

		public TableReducer()
		{
			nodes = new List<INode>();
		}

		/// <summary>
		/// Returns a DIV will all the children pulled out of the layout table
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public IHtmlElement ConvertTable(IHtmlTableElement table)
		{
			foreach(var row in table.Rows)
			{
				foreach(var col in row.Cells)
				{
					ReduceCell(col);
				}
			}

			var div = table.Owner.CreateElement<IHtmlDivElement>();
			nodes.ForEach(x => div.AppendChild(x));
			return div;
		}


		private void ReduceCell(IHtmlTableCellElement cell)
			=> nodes.AddRange(cell.ChildNodes);

		/// <summary>
		/// determine if this table is just used for layout purposes
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public static bool IsLayoutTable(IHtmlTableElement table)
		{
			if(table.Rows.Length == 1)
			{
				return true;
			}
			if(table.Rows.Length <= 3 && table.Rows[0].Cells.Length == 1)
			{
				return true;
			}
			return false;
		}
	}
}
