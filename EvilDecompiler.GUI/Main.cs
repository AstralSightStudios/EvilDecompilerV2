using AntdUI;
using EvilDecompiler.JsObject;
using EvilDecompiler.JsObject.Types.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EvilDecompiler.GUI
{
    public partial class Main : FormAutoSize
    {

        JsObjectReader reader;

        public Main()
        {
            InitializeComponent();
            SetAutoSize();
        }

        private void InputFileName_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Title = "请选择文件";
            fileDialog.Filter = "QuickJs字节码文件(*.jsc)|*.jsc";
            fileDialog.Multiselect = false;

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                InputFileName.Text = fileDialog.FileNames[0];
            }
        }

        void ProcessNode(JsonNode node, TreeItem parent)
        {
            if (node.GetValueKind() == JsonValueKind.Object)
            {
                foreach (var kvp in node.AsObject())
                {
                    var key = kvp.Key.ToString();
                    var value = kvp.Value;

                    var treeItem = new TreeItem(key);
                    parent.Sub.Add(treeItem);

                    if (value != null)
                    {
                        if (value.GetValueKind() == JsonValueKind.Object || value.GetValueKind() == JsonValueKind.Array)
                        {
                            treeItem.Tag = value.ToString();
                            ProcessNode(value, treeItem);
                        }
                        else
                        {
                            treeItem.Tag = value.ToString();
                        }
                    }
                }
            }
            else if (node.GetValueKind() == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in node.AsArray())
                {
                    var treeItem = new TreeItem($"Item {index++}");
                    parent.Sub.Add(treeItem);

                    if (item != null)
                    {
                        if (item.GetValueKind() == JsonValueKind.Object || item.GetValueKind() == JsonValueKind.Array)
                        {
                            treeItem.Tag = item.ToString();
                            ProcessNode(item, treeItem);
                        }
                        else
                        {
                            treeItem.Tag = item.ToString();
                        }
                    }
                }
            }
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            string path = InputFileName.Text;

            if (File.Exists(path))
            {
                reader = new JsObjectReader(new MemoryStream(File.ReadAllBytes(path)));
                if (reader.JsObject != null)
                {
                    string json = JsonConvert.SerializeObject(reader.JsObject);
                    JsonNode? node = JsonNode.Parse(json);

                    if (node != null)
                    {
                        JsTree.Items.Clear();
                        var rootNode = new TreeItem("File").SetExpand();
                        ProcessNode(node, rootNode);
                        JsTree.Items.Add(rootNode);
                    }

                }


                DataTable dt = new DataTable();
                dt.Columns.Add("ID");
                dt.Columns.Add("String");

                if (reader.Atoms != null)
                {
                    for (int i = 0; i < reader.Atoms.Atoms.Count; i++)
                    {
                        int index = i + 1;
                        if (!reader.Atoms.IsInternal(index))
                        {
                            JsString? str = reader.Atoms.Get(index);
                            if (str != null) {
                                dt.Rows.Add(index.ToString(), str.Value);
                            }
                        }
                    }
                }
                

                StringTable.DataSource = dt;

            }
        }

        private void JsTree_SelectChanged(object sender, TreeSelectEventArgs e)
        {
            string data = (string)(e.Item.Tag ?? "");
            Editor.Text = data;
        }
    }
}
