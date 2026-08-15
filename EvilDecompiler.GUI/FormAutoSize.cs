using UCControl = System.Windows.Forms.Control;

namespace EvilDecompiler.GUI
{


    /// <summary>
    /// 控件自动适应
    /// 使用直接继承 FormAutoSize即可
    /// 示例如下：
    /// ***示例代码***：public Form1()
    /// ***示例代码***：{
    /// ***示例代码***：       InitializeComponent();
    /// ***示例代码***：       SetAutoSize();
    /// ***示例代码***： }
    /// </summary>
    public class FormAutoSize : Form
    {
        public FormAutoSize() : base()
        {
            this.Resize += Form1_Resize;
        }
        /// <summary>
        /// 设置控件大小随窗体大小等比例缩放
        /// </summary>
        public void SetAutoSize()
        {
            XClientRectangleWidth = this.ClientRectangle.Width; //this.Width; ClientRectangleWidth
            YClientRectangleHeight = this.ClientRectangle.Height;// this.Height;
            SetTag(this);
        }
        #region 控件大小随窗体大小等比例缩放
        private float XClientRectangleWidth;//定义当前窗体的工作区域宽度
        private float YClientRectangleHeight;//定义当前窗体的工作区域高度
        private void SetTag(UCControl cons)
        {
            foreach (UCControl con in cons.Controls)
            {
                con.Tag = con.Width + ";" + con.Height + ";" + con.Left + ";" + con.Top + ";" + con.Font.Size;
                if (con.Controls.Count > 0)
                {
                    SetTag(con);
                }
            }
        }
        private void setControls(float newx, float newy, UCControl cons)
        {
            //遍历窗体中的控件，重新设置控件的值
            foreach (UCControl con in cons.Controls)
            {
                //获取控件的Tag属性值，并分割后存储字符串数组
                if (con.Tag != null)
                {
                    string[] mytag = con.Tag.ToString().Split(new char[] { ';' });
                    //根据窗体缩放的比例确定控件的值
                    con.Width = Convert.ToInt32(System.Convert.ToSingle(mytag[0]) * newx);//宽度
                    con.Height = Convert.ToInt32(System.Convert.ToSingle(mytag[1]) * newy);//高度
                    con.Left = Convert.ToInt32(System.Convert.ToSingle(mytag[2]) * newx);//左边距
                    con.Top = Convert.ToInt32(System.Convert.ToSingle(mytag[3]) * newy);//顶边距
                    Single currentSize = System.Convert.ToSingle(mytag[4]) * newy;//字体大小
                    con.Font = new Font(con.Font.Name, currentSize == 0 ? 1 : currentSize, con.Font.Style, con.Font.Unit);
                    Console.WriteLine($"setControls={con.Name}");
                    if (con.Controls.Count > 0)
                    {
                        setControls(newx, newy, con);
                    }
                }
            }
        }
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }
            float newx = (this.ClientRectangle.Width) / XClientRectangleWidth;
            float newy = (this.ClientRectangle.Height) / YClientRectangleHeight;

            this.SuspendLayout();//临时挂起控件的布局逻辑。 
            setControls(newx, newy, this);
            this.ResumeLayout(false);//恢复正常的布局逻辑，可以选择强制对挂起的布局请求立即进行布局。
        }

        #endregion
    }



    /// <summary> 
    /// 指定控件(panel)绽放
    ///  cas = new ControlAutoSize(panel1);
    ///  cas._SetAutoSize();
    /// </summary>
    public class ControlAutoSize
    {
        private UCControl uccontrol;
        public ControlAutoSize(UCControl control)// : base()
        {
            uccontrol = control;
            control.Resize += Control1_Resize;
        }
        /// <summary>
        /// 设置控件大小随窗体大小等比例缩放
        /// </summary>
        public void _SetAutoSize()
        {
            XClientRectangleWidth = uccontrol.ClientRectangle.Width; //control.Width; ClientRectangleWidth
            YClientRectangleHeight = uccontrol.ClientRectangle.Height;// control.Height;
            SetTag(uccontrol);
        }
        #region 控件大小随窗体大小等比例缩放
        private float XClientRectangleWidth;//定义当前窗体的工作区域宽度
        private float YClientRectangleHeight;//定义当前窗体的工作区域高度
        private void SetTag(UCControl cons)
        {
            foreach (UCControl con in cons.Controls)
            {
                con.Tag = con.Width + ";" + con.Height + ";" + con.Left + ";" + con.Top + ";" + con.Font.Size;
                if (con.Controls.Count > 0)
                {
                    SetTag(con);
                }
            }
        }
        private void setControls(float newx, float newy, UCControl cons)
        {
            //遍历窗体中的控件，重新设置控件的值
            foreach (UCControl con in cons.Controls)
            {
                //获取控件的Tag属性值，并分割后存储字符串数组
                if (con.Tag != null)
                {
                    string[] mytag = con.Tag.ToString().Split(new char[] { ';' });
                    //根据窗体缩放的比例确定控件的值
                    con.Width = Convert.ToInt32(System.Convert.ToSingle(mytag[0]) * newx);//宽度
                    con.Height = Convert.ToInt32(System.Convert.ToSingle(mytag[1]) * newy);//高度
                    con.Left = Convert.ToInt32(System.Convert.ToSingle(mytag[2]) * newx);//左边距
                    con.Top = Convert.ToInt32(System.Convert.ToSingle(mytag[3]) * newy);//顶边距
                    Single currentSize = System.Convert.ToSingle(mytag[4]) * newy;//字体大小
                    con.Font = new Font(con.Font.Name, currentSize, con.Font.Style, con.Font.Unit);
                    Console.WriteLine($"setControls={con.Name}");
                    if (con.Controls.Count > 0)
                    {
                        setControls(newx, newy, con);
                    }
                }
            }
        }
        private void Control1_Resize(object sender, EventArgs e)
        {
            if (uccontrol.FindForm().WindowState != FormWindowState.Minimized)
            {//修复最小化异常
                float newx = (uccontrol.ClientRectangle.Width) / XClientRectangleWidth;
                float newy = (uccontrol.ClientRectangle.Height) / YClientRectangleHeight;

                uccontrol.SuspendLayout();//临时挂起控件的布局逻辑。 
                setControls(newx, newy, uccontrol);
                uccontrol.ResumeLayout(false);//恢复正常的布局逻辑，可以选择强制对挂起的布局请求立即进行布局。
            }
        }

        #endregion
    }

}
