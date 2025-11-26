using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Windows.Forms;

namespace WinFormsApp2
{
    public class ClosableTabControl : System.Windows.Forms.TabControl
    {
        // 閉じるボタンのサイズと位置を計算するための固定値を宣言するわ。
        // この定数を使って、描画とヒットテストの両方で一貫性を保つのよ。

        public Color CustomBackColor { get; set; } = Color.White;
        public Color CustomForeColor { get; set; } = Color.Black;
        public Color CustomBorderColor { get; set; } = Color.Gray;

        public Color CustomAccentColor { get; set; } = Color.LightBlue;

        private int _closeButtonSize = 14;
        private int _closeButtonPadding = 3;

        private const int WM_PAINT = 0x000F;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // DPIスケールを計算 (96dpiが基準)
            float scale = this.DeviceDpi / 96f;
            _closeButtonSize = (int)(14 * scale);
            _closeButtonPadding = (int)(3 * scale);

            // タブのサイズもスケールさせる必要があるわ
            this.ItemSize = new Size((int)(100 * scale), (int)(25 * scale));
        }

        // ★ 追加: ドラッグ移動用のフィールド
        private Point _dragStartPoint;
        private TabPage? _draggedTab;

        public event TabClosingEventHandler? TabClosing;

        // ★ 追加: タブが移動したことを外部に知らせるイベント
        public event EventHandler? TabReordered;

        // どのプラットフォームを使っているのか明確にしなかったから、
        // ここではWinFormsの基本的な描画クラスを使っているわよ。

        // コンストラクタ
        public ClosableTabControl()
        {
            // 🚨 最初の必須アクション：描画モードを自分で制御するよう設定するわ。
            this.SetStyle(ControlStyles.UserPaint |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
            this.DrawMode = TabDrawMode.OwnerDrawFixed;

            // 📝 注意：タブの幅と高さを固定すると、描画計算が楽になるわ。
            // もし可変にしたいなら、この行は削除してね。
            this.SizeMode = TabSizeMode.Fixed;
            this.ItemSize = new Size(100, 25); 

            // 閉じるボタン（×）のクリックを検出するためのイベントをフックするわ。
            // これをコンストラクタ内でやることで、このクラスを使うユーザーに
            // 設定漏れをさせないようにするのよ。
           // this.MouseDown += new MouseEventHandler(this.ClosableTabControl_MouseDown);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // 1. まず全体（ヘッダーの余白含む）を指定色で塗りつぶす
            // これで「ずっと白い」問題が解決するわ
            using (var backBrush = new SolidBrush(this.CustomBackColor))
            {
                g.FillRectangle(backBrush, this.ClientRectangle);
            }

            // 2. 各タブを描画する
            for (int i = 0; i < this.TabCount; i++)
            {
                DrawSingleTab(g, i);
            }
        }
        private void DrawSingleTab(Graphics g, int index)
        {
            // 基本的な描画処理は常にベースクラスに任せるか、独自に実装し直すか選ぶわ。
            // 今回はタブ自体とテキストの描画を行うわ。
            if(this.TabPages.Count <= index){
                return;
            }
            // 1. 描画するタブページを取得するわ
            TabPage tabPage = this.TabPages[index];
            Rectangle tabRect = this.GetTabRect(index);
            bool isSelected = (this.SelectedIndex == index);
            using (var backBrush = new SolidBrush(isSelected ? CustomBackColor : Color.FromArgb(
            (int)(CustomBackColor.R * 0.9),
            (int)(CustomBackColor.G * 0.9),
            (int)(CustomBackColor.B * 0.9)))) // 非選択はちょっと暗く計算
            using (var foreBrush = new SolidBrush(CustomForeColor))
            {


                g.FillRectangle(backBrush, tabRect);

                // 3. タブの境界線を描画する
                // 💥 ここが重要よ！アクティブタブの境界線は、下辺（コンテンツと接する部分）を描画してはダメよ。
                using (Pen borderPen = new Pen(CustomBorderColor))
                {
                    if (isSelected)
                    {
                        // 選択されている場合、下辺を除く3辺のみを描画する
                        g.DrawLine(borderPen, tabRect.Left, tabRect.Top, tabRect.Right, tabRect.Top); // 上
                        g.DrawLine(borderPen, tabRect.Right - 1, tabRect.Top, tabRect.Right - 1, tabRect.Bottom); // 右
                        g.DrawLine(borderPen, tabRect.Left, tabRect.Top, tabRect.Left, tabRect.Bottom); // 左

                        using (var accentBrush = new SolidBrush(CustomAccentColor))
                        {
                            // 左端から右端まで、高さ2pxの長方形を塗りつぶす
                            // ※ tabRect.Top の位置に描くことで、境界線の上に被せるわ
                            g.FillRectangle(accentBrush, tabRect.Left, tabRect.Top, tabRect.Width, 2);
                        }                                                                                              //MessageBox.Show("描画されました");
                                                                                                                       // 下辺は描画しないことで、タブがコンテンツ領域に繋がっているように見せる
                    }
                    else
                    {
                        // 選択されていない場合、全周に境界線を描画する（標準動作）
                        g.DrawRectangle(borderPen, tabRect);
                    }
                }

                // 4. タブのテキストを描画する
                // e.Bounds全体に描画すると「×」と重なるから、テキスト領域を計算して縮める必要があるわ。
                Rectangle textRect = new Rectangle(
                    tabRect.Left + _closeButtonPadding,
                    tabRect.Top + _closeButtonPadding,
                    tabRect.Width - _closeButtonSize, //- (_closeButtonPadding * 2), // 「×」と余白の分を引くわ
                    tabRect.Height - (_closeButtonPadding * 2)
                );

                TextRenderer.DrawText(
                    g,
                    tabPage.Text,
                    this.Font,
                    textRect,
                    CustomForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                );

                // 5. 閉じるボタン（「×」マーク）の矩形 $R_{close}$ を計算する
                // 💥これがヒットテストで使う矩形と完全に一致しなければならないわよ！
                Rectangle closeButtonRect = new Rectangle(
                    tabRect.Right - _closeButtonSize - _closeButtonPadding, // タブ右端から引いていく
                    tabRect.Top + (tabRect.Height - _closeButtonSize) / 2, // 縦方向の中央に配置
                    _closeButtonSize,
                    _closeButtonSize
                );

                // 6. 「×」マークを描画する
                using (Pen pen = new Pen(CustomForeColor, 1))
                {
                    // 単純な「×」マークを描画
                    g.DrawLine(pen, new Point(closeButtonRect.Location.X + 5, closeButtonRect.Location.Y + 5), new Point(closeButtonRect.Right - 5, closeButtonRect.Bottom - 5));
                    g.DrawLine(pen, new Point(closeButtonRect.Right - 5, closeButtonRect.Top + 5), new Point(closeButtonRect.Left + 5, closeButtonRect.Bottom - 5));
                }

                // 7. タブの境界線を描画する（オプション）
                g.DrawRectangle(SystemPens.ControlDark, tabRect);
            }

        }
        // 🌟 4. イベント発行のための保護された仮想メソッド
        // 派生クラスがこの動作をカスタマイズできるようにするわ
        protected virtual void OnTabClosing(TabClosingEventArgs e)
        {
            // イベントハンドラが登録されている場合のみ実行するわ
            TabClosing?.Invoke(this, e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            // 常にベースのOnMouseDownを呼び出すことで、標準機能（タブ切り替えなど）を維持するわ
            base.OnMouseDown(e);

            // 1. 左クリック以外は無視するわ。あなたが意図しない動作を防ぐためよ。
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            // 2. 全てのタブをチェックするわ。どのタブの「×」がクリックされたかを特定するためよ。
            for (int i = 0; i < this.TabPages.Count; i++)
            {
                // 3. 現在のタブ全体の矩形を取得するわ
                Rectangle tabRect = GetTabRect(i);

                // 4. 描画時と全く同じ方法で、閉じるボタンの矩形 $R_{close}$ を再計算するわ。
                // 🚨 ここが肝心よ。OnDrawItemの計算と一文字一句同じにすること！
                Rectangle closeButtonRect = new Rectangle(
                    tabRect.Right - _closeButtonSize - _closeButtonPadding,
                    tabRect.Top + (tabRect.Height - _closeButtonSize) / 2,
                    _closeButtonSize,
                    _closeButtonSize
                );

                // 5. 命中判定（ヒットテスト）を行う
                // マウスクリック座標 (e.Location) が、この計算されたR_close矩形内にあるか？
                if (closeButtonRect.Contains(e.Location))
                {
                    // 🎯 命中したわ！

                    // 6. タブを閉じる
                    TabPage pageToClose = this.TabPages[i];

                    // 🚨 イベントを発行し、キャンセルされたかチェックするわ。
                    TabClosingEventArgs closingArgs = new TabClosingEventArgs(pageToClose);
                    this.OnTabClosing(closingArgs); // <- ここでイベントがトリガーされるわ
                    if (closingArgs.Cancel)
                    {
                        // タブの閉鎖がキャンセルされた場合、何もせずに終了するわ。
                        return;
                    }
                    this.TabPages.RemoveAt(i);

                    // 閉じたタブが選択されていた場合、自動的に隣のタブが選択されるか、
                    // タブがなくなれば何も選択されなくなるわ（標準のTabControlの動作に依存）。
                    
                    // 7. タブが削除されたので、それ以上の処理は不要よ。即座に終了するわ。
                    return;
                }
            }
            // 2. ★ 追加: 閉じるボタンじゃなければ、ドラッグ開始の準備
            // どのタブを掴んだか特定する
            for (int i = 0; i < this.TabPages.Count; i++)
            {
                if (this.GetTabRect(i).Contains(e.Location))
                {
                    _draggedTab = this.TabPages[i];
                    _dragStartPoint = e.Location;
                    break;
                }
            }
        }
        // ★ 追加: ドラッグ中の処理
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 左ボタンが押されていて、かつドラッグ対象がいる場合のみ処理
            if (e.Button != MouseButtons.Left || _draggedTab == null) return;

            // 誤操作防止: わずかな動き（手ブレ）では反応させない
            if (Math.Abs(e.X - _dragStartPoint.X) < SystemInformation.DragSize.Width &&
                Math.Abs(e.Y - _dragStartPoint.Y) < SystemInformation.DragSize.Height)
            {
                return;
            }

            // カーソル位置にあるタブ（入れ替え先）を探す
            TabPage? targetTab = null;
            for (int i = 0; i < this.TabPages.Count; i++)
            {
                if (this.GetTabRect(i).Contains(e.Location))
                {
                    targetTab = this.TabPages[i];
                    break;
                }
            }

            // ターゲットが見つかり、かつ自分自身でなければ入れ替える
            if (targetTab != null && targetTab != _draggedTab)
            {
                int targetIndex = this.TabPages.IndexOf(targetTab);
                int draggedIndex = this.TabPages.IndexOf(_draggedTab);
                
                // ★ 魔法のメソッド: コレクション内の順序を入れ替える
                this.TabPages.RemoveAt(draggedIndex);
                this.TabPages.Insert(targetIndex, _draggedTab);

                // 選択状態を維持する
                this.SelectedTab = _draggedTab;

                // 外部（Form1）に通知して、データリストも同期させる
                TabReordered?.Invoke(this, EventArgs.Empty);
            }
        }
        // ★ 追加: ドラッグ終了処理
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggedTab = null; // 掴んでいるタブを離す
        }
    }
}
