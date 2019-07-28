using Gibbed.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    public class Bio2DA
    {
        public bool IsIndexed = false;
        /// <summary>
        /// Number of cells that the Bio2DA says exist in the table
        /// </summary>
        int PopulatedCellCount = 0;
        static string[] stringRefColumns = { "StringRef", "SaveGameStringRef", "Title", "LabelRef", "Name", "ActiveWorld", "Description", "ButtonLabel" };
        public Bio2DACell[,] Cells { get; set; }
        public List<string> RowNames { get; set; }
        public List<string> ColumnNames { get; set; }

        public int RowCount => RowNames?.Count ?? 0;

        public int ColumnCount => ColumnNames?.Count ?? 0;

        IExportEntry export;
        public Bio2DA(IExportEntry export)
        {
            Console.WriteLine("Loading " + export.ObjectName);
            this.export = export;
            ME1Package pcc = export.FileRef;
            byte[] data = export.Data;

            RowNames = new List<string>();
            if (export.ClassName == "Bio2DA")
            {
                const string rowLabelsVar = "m_sRowLabel";
                var props = export.GetProperty<ArrayProperty<NameProperty>>(rowLabelsVar);
                if (props != null)
                {
                    foreach (NameProperty n in props)
                    {
                        RowNames.Add(n.ToString());
                    }
                }
                else
                {
                    Console.WriteLine("Unable to find row names property!");
                    Debugger.Break();
                    return;
                }
            }
            else
            {
                var props = export.GetProperty<ArrayProperty<IntProperty>>("m_lstRowNumbers");//Bio2DANumberedRows
                if (props != null)
                {
                    foreach (IntProperty n in props)
                    {
                        RowNames.Add(n.Value.ToString());
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property (m_lstRowNumbers)!");
                    Debugger.Break();
                    return;
                }
            }

            //Get Columns
            ColumnNames = new List<string>();
            int colcount = BitConverter.ToInt32(data, data.Length - 4);
            int currentcoloffset = 0;
            while (colcount >= 0)
            {
                currentcoloffset += 4;
                int colindex = BitConverter.ToInt32(data, data.Length - currentcoloffset);
                currentcoloffset += 8; //names in this case don't use nameindex values.
                int nameindex = BitConverter.ToInt32(data, data.Length - currentcoloffset);
                string name = pcc.getNameEntry(nameindex);
                ColumnNames.Insert(0, name);
                colcount--;
            }
            Cells = new Bio2DACell[RowCount, ColumnCount];

            currentcoloffset += 4;  //column count.
            int infilecolcount = BitConverter.ToInt32(data, data.Length - currentcoloffset);

            //start of binary data
            int binstartoffset = export.propsEnd(); //arrayheader + nonenamesize + number of items in this list
            int curroffset = binstartoffset;

            int cellcount = BitConverter.ToInt32(data, curroffset);
            if (cellcount > 0)
            {
                curroffset += 4;
                for (int rowindex = 0; rowindex < RowCount; rowindex++)
                {
                    for (int colindex = 0; colindex < ColumnCount && curroffset < data.Length - currentcoloffset; colindex++)
                    {
                        byte dataType = data[curroffset];
                        curroffset++;
                        int dataSize = dataType == (byte)Bio2DACell.Bio2DADataType.TYPE_NAME ? 8 : 4;
                        byte[] celldata = new byte[dataSize];
                        Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                        Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                        Cells[rowindex, colindex] = cell;
                        curroffset += dataSize;
                    }
                }
                PopulatedCellCount = RowCount * ColumnCount;  //Required for edits to write correct count if SaveToExport
            }
            else
            {
                IsIndexed = true;
                curroffset += 4; //theres a 0 here for some reason
                cellcount = BitConverter.ToInt32(data, curroffset);
                curroffset += 4;

                //curroffset += 4;
                while (PopulatedCellCount < cellcount)
                {
                    int index = BitConverter.ToInt32(data, curroffset);
                    int row = index / ColumnCount;
                    int col = index % ColumnCount;
                    curroffset += 4;
                    byte dataType = data[curroffset];
                    int dataSize = dataType == (byte)Bio2DACell.Bio2DADataType.TYPE_NAME ? 8 : 4;
                    curroffset++;
                    var celldata = new byte[dataSize];
                    Buffer.BlockCopy(data, curroffset, celldata, 0, dataSize);
                    Bio2DACell cell = new Bio2DACell(pcc, curroffset, dataType, celldata);
                    this[row, col] = cell;
                    curroffset += dataSize;
                }
            }
            //Console.WriteLine("Finished loading " + export.ObjectName);
        }

        /// <summary>
        /// Initializes a blank Bio2DA. Cells is not initialized, the caller must set up this Bio2DA.
        /// </summary>
        public Bio2DA()
        {
            ColumnNames = new List<string>();
            RowNames = new List<string>();
        }

        /// <summary>
        /// Adds a row to the table.
        /// Not the most efficient way to do this.
        /// </summary>
        /// <param name="rowName"></param>
        public void AddRow(string rowName = null)
        {
            var newCells = new Bio2DACell[RowCount + 1, ColumnCount];
            for (int i = 0; i < RowCount; i++)
            {
                for (int j = 0; j < ColumnCount; j++)
                {
                    newCells[i, j] = Cells[i, j];
                }
            }

            if (rowName != null)
            {
                RowNames.Add(rowName);
            }
            else
            {
                RowNames.Add((int.Parse(RowNames.Last()) + 1).ToString()); //indexed by number
            }

            Cells = newCells;
        }

        internal string GetColumnNameByIndex(int columnIndex)
        {
            if (columnIndex < ColumnNames.Count && columnIndex >= 0)
            {
                return ColumnNames[columnIndex];
            }
            return null;
        }

        public Bio2DACell GetColumnItem(int row, string columnName)
        {
            int colIndex = ColumnNames.IndexOf(columnName);
            if (colIndex >= 0)
            {
                return Cells[row, colIndex];
            }
            return null;
        }

        public int GetColumnIndexByName(string columnName)
        {
            return ColumnNames.IndexOf(columnName);
        }

        public void Write2DAToExport()
        {
            using (var stream = new MemoryStream())
            {
                //Cell count
                if (IsIndexed)
                {
                    //Indexed ones seem to have 0 at start
                    stream.WriteBytes(BitConverter.GetBytes(0));
                }
                stream.WriteBytes(BitConverter.GetBytes(PopulatedCellCount));

                //Write cell data
                for (int rowindex = 0; rowindex < RowCount; rowindex++)
                {
                    for (int colindex = 0; colindex < ColumnCount; colindex++)
                    {
                        Bio2DACell cell = Cells[rowindex, colindex];
                        if (cell != null)
                        {
                            if (IsIndexed)
                            {
                                //write index
                                int index = (rowindex * ColumnCount) + colindex; //+1 because they are not zero based indexes since they are numerals
                                stream.WriteBytes(BitConverter.GetBytes(index));
                            }
                            stream.WriteByte((byte)cell.Type);
                            stream.WriteBytes(cell.Data);
                        }
                        else
                        {
                            if (IsIndexed)
                            {
                                //this is a blank cell. It is not present in the table.
                                continue;
                            }
                            else
                            {
                                Debug.WriteLine("THIS SHOULDN'T OCCUR!");
                                Debugger.Break();
                                throw new Exception("A non-indexed Bio2DA cannot have null cells.");
                            }
                        }
                    }
                }

                //Write Columns
                if (!IsIndexed)
                {
                    stream.WriteBytes(BitConverter.GetBytes(0)); //seems to be a 0 before column definitions
                }
                //Console.WriteLine("Columns defs start at " + stream.Position.ToString("X6"));
                stream.WriteBytes(BitConverter.GetBytes(ColumnCount));
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    //Console.WriteLine("Writing column definition " + columnNames[colindex]);
                    int nameIndexForCol = export.FileRef.FindNameOrAdd(ColumnNames[colindex]);
                    stream.WriteBytes(BitConverter.GetBytes(nameIndexForCol));
                    stream.WriteBytes(BitConverter.GetBytes(0)); //second half of name reference in 2da is always zero since they're always indexed at 0
                    stream.WriteBytes(BitConverter.GetBytes(colindex));
                }

                int propsEnd = export.propsEnd();
                byte[] binarydata = stream.ToArray();

                //Todo: Rewrite properties here
                PropertyCollection props = new PropertyCollection();
                if (export.ClassName == "Bio2DA")
                {
                    var indicies = new ArrayProperty<NameProperty>(ArrayType.Name, "m_sRowLabel");
                    foreach (var rowname in RowNames)
                    {
                        indicies.Add(new NameProperty { Value = rowname });
                    }
                    props.Add(indicies);
                }
                else
                {
                    var indices = new ArrayProperty<IntProperty>(ArrayType.Int, "m_lstRowNumbers");
                    foreach (var rowname in RowNames)
                    {
                        indices.Add(new IntProperty(int.Parse(rowname)));
                    }
                    props.Add(indices);
                }

                MemoryStream propsStream = new MemoryStream();
                props.WriteTo(propsStream, export.FileRef);
                MemoryStream currentDataStream = new MemoryStream(export.Data);
                byte[] propertydata = propsStream.ToArray();
                int propertyStartOffset = export.GetPropertyStart();
                var newExportData = new byte[propertyStartOffset + propertydata.Length + binarydata.Length];
                Buffer.BlockCopy(export.Data, 0, newExportData, 0, propertyStartOffset);
                propertydata.CopyTo(newExportData, propertyStartOffset);
                binarydata.CopyTo(newExportData, propertyStartOffset + propertydata.Length);
                //Console.WriteLine("Old data size: " + export.Data.Length);
                //Console.WriteLine("NEw data size: " + newExportData.Length);

                //This assumes the input and output data sizes are the same. We should not assume this with new functionality
                //if (export.Data.Length != newExportData.Length)
                //{
                //    Debug.WriteLine("FILES ARE WRONG SIZE");
                //    Debugger.Break();
                //}
                export.Data = newExportData;
            }
        }

        public Bio2DACell this[int rowindex, int colindex]
        {
            get => Cells[rowindex, colindex];
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (Cells[rowindex, colindex] == null && value != null)
                {
                    PopulatedCellCount++;
                }
                if (Cells[rowindex, colindex] != null && value == null)
                {
                    PopulatedCellCount--;
                }
                Cells[rowindex, colindex] = value;
            }
        }

        public Bio2DACell this[int rowindex, string columnName]
        {
            get => Cells[rowindex, GetColumnIndexByName(columnName)];
            set
            {
                int colindex = GetColumnIndexByName(columnName);
                // set the item for this index. value will be of type Bio2DACell.
                if (Cells[rowindex, colindex] == null && value != null)
                {
                    PopulatedCellCount++;
                }
                if (Cells[rowindex, colindex] != null && value == null)
                {
                    PopulatedCellCount--;
                }
                Cells[rowindex, colindex] = value;
            }
        }

        public int GetRowIndexByName(int rowName)
        {
            return GetRowIndexByName(rowName.ToString());
        }

        public int GetRowIndexByName(string rowName)
        {
            return RowNames.IndexOf(rowName);
        }
    }
}
