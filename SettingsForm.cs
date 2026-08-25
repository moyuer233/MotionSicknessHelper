using System.Data;

namespace MotionSicknessHelper;

internal sealed class SettingsForm : Form
{
    private readonly string _configPath;
    private readonly DataGridView _grid = new();
    private readonly NumericUpDown _edgeInset = new() { Minimum = 0, Maximum = 200, Value = 8 };
    private readonly NumericUpDown _flashInterval = new() { Minimum = 50, Maximum = 10000, Increment = 50, Value = 500 };
    private readonly CheckBox _flashAllCheck = new() { Text = "一键启用闪烁", AutoSize = true, Margin = new Padding(12, 6, 3, 0) };
    private bool _loading;
    private DataGridViewComboBoxColumn? _colPosition;
    private DataGridViewComboBoxColumn? _colShape;
    private DataGridViewTextBoxColumn? _colSize;
    private DataGridViewTextBoxColumn? _colThickness;
    private DataGridViewTextBoxColumn? _colColor;
    private DataGridViewCheckBoxColumn? _colFlash;
    private DataGridViewTextBoxColumn? _colColor2;
    private DataGridViewTextBoxColumn? _colOpacity;

    public OverlayConfig? Result { get; private set; }

    /// <summary>Raised when the user clicks "应用" so the overlay can update without closing the window.</summary>
    public event Action<OverlayConfig>? Applied;

    public SettingsForm(OverlayConfig config, string configPath)
    {
        _configPath = configPath;
        Text = "晕3D辅助 - 设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(880, 460);
        Size = new Size(980, 520);
        TopMost = true;

        BuildUi();
        LoadConfig(config);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // Top toolbar
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 6)
        };

        var btnCorners = new Button { Text = "四角预设", AutoSize = true };
        btnCorners.Click += (_, _) => SetPreset(Preset.Corners);

        var btnEdges = new Button { Text = "四边中点预设", AutoSize = true };
        btnEdges.Click += (_, _) => SetPreset(Preset.Edges);

        var btnClear = new Button { Text = "清空", AutoSize = true };
        btnClear.Click += (_, _) => _grid.Rows.Clear();

        var btnAdd = new Button { Text = "添加图形", AutoSize = true };
        btnAdd.Click += (_, _) => AddRow();

        var btnRemove = new Button { Text = "删除选中", AutoSize = true };
        btnRemove.Click += (_, _) => RemoveSelected();

        var btnColor = new Button { Text = "选色...", AutoSize = true };
        btnColor.Click += (_, _) => PickColorForSelected();

        toolbar.Controls.Add(btnCorners);
        toolbar.Controls.Add(btnEdges);
        toolbar.Controls.Add(btnClear);
        toolbar.Controls.Add(btnAdd);
        toolbar.Controls.Add(btnRemove);
        toolbar.Controls.Add(btnColor);
        toolbar.Controls.Add(new Label { Text = "边缘间距:", AutoSize = true, Margin = new Padding(12, 6, 3, 0) });
        toolbar.Controls.Add(_edgeInset);
        toolbar.Controls.Add(new Label { Text = "闪烁间隔(ms):", AutoSize = true, Margin = new Padding(12, 6, 3, 0) });
        toolbar.Controls.Add(_flashInterval);
        _flashAllCheck.CheckedChanged += (_, _) =>
        {
            if (!_loading)
                SetAllFlash(_flashAllCheck.Checked);
        };
        toolbar.Controls.Add(_flashAllCheck);

        root.Controls.Add(toolbar, 0, 0);

        // Grid
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;

        _colPosition = new DataGridViewComboBoxColumn
        {
            HeaderText = "位置",
            DataSource = Enum.GetValues(typeof(AnchorPosition)),
            ValueType = typeof(AnchorPosition),
            FillWeight = 18,
            FlatStyle = FlatStyle.Flat
        };

        _colShape = new DataGridViewComboBoxColumn
        {
            HeaderText = "形状",
            DataSource = Enum.GetValues(typeof(ShapeKind)),
            ValueType = typeof(ShapeKind),
            FillWeight = 12,
            FlatStyle = FlatStyle.Flat
        };

        _colSize = new DataGridViewTextBoxColumn { HeaderText = "长度", FillWeight = 12 };
        _colThickness = new DataGridViewTextBoxColumn { HeaderText = "粗细", FillWeight = 12 };
        _colColor = new DataGridViewTextBoxColumn { HeaderText = "颜色(#RRGGBB)", FillWeight = 20 };
        _colFlash = new DataGridViewCheckBoxColumn { HeaderText = "闪烁", FillWeight = 8 };
        _colColor2 = new DataGridViewTextBoxColumn { HeaderText = "第二颜色(#RRGGBB)", FillWeight = 20 };
        _colOpacity = new DataGridViewTextBoxColumn { HeaderText = "不透明度0-255", FillWeight = 15 };

        _grid.Columns.AddRange(_colPosition, _colShape, _colSize, _colThickness, _colColor, _colFlash, _colColor2, _colOpacity);
        _grid.DataError += OnGridDataError;
        root.Controls.Add(_grid, 0, 1);

        // Bottom buttons
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 6, 0, 0)
        };

        var btnCancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        var btnApply = new Button { Text = "应用", AutoSize = true };
        btnApply.Click += (_, _) => ApplyChanges();

        var btnSave = new Button { Text = "保存并应用", AutoSize = true };
        btnSave.Click += (_, _) => SaveAndClose();

        bottom.Controls.Add(btnSave);
        bottom.Controls.Add(btnApply);
        bottom.Controls.Add(btnCancel);
        root.Controls.Add(bottom, 0, 2);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private enum Preset
    {
        Corners,
        Edges
    }

    private void LoadConfig(OverlayConfig config)
    {
        _loading = true;
        try
        {
            _edgeInset.Value = Math.Clamp(config.EdgeInset, _edgeInset.Minimum, _edgeInset.Maximum);
            _flashInterval.Value = Math.Clamp(config.FlashIntervalMs, _flashInterval.Minimum, _flashInterval.Maximum);
            foreach (var shape in config.Shapes)
                AddRow(shape);

            _flashAllCheck.Checked = config.Shapes.Count > 0 && config.Shapes.All(s => s.FlashEnabled);
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddRow(ShapeConfig? shape = null)
    {
        shape ??= new ShapeConfig();
        int index = _grid.Rows.Add(
            shape.Position,
            shape.Shape,
            shape.Size,
            shape.Thickness,
            shape.Color,
            shape.FlashEnabled,
            shape.Color2,
            shape.Opacity);

        _grid.Rows[index].Tag = shape.Clone();

        if (_flashAllCheck.Checked && _colFlash is not null)
            _grid.Rows[index].Cells[_colFlash.Index].Value = true;
    }

    private void RemoveSelected()
    {
        if (_grid.CurrentRow is { } row && !row.IsNewRow)
            _grid.Rows.Remove(row);
    }

    private void SetAllFlash(bool enabled)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (!row.IsNewRow && _colFlash is not null)
                row.Cells[_colFlash.Index].Value = enabled;
        }
    }

    private void SetPreset(Preset preset)
    {
        _grid.Rows.Clear();
        if (preset == Preset.Corners)
        {
            AddRow(new ShapeConfig { Position = AnchorPosition.TopLeft, Size = 240, Thickness = 50 });
            AddRow(new ShapeConfig { Position = AnchorPosition.TopRight, Size = 240, Thickness = 50 });
            AddRow(new ShapeConfig { Position = AnchorPosition.BottomLeft, Size = 240, Thickness = 50 });
            AddRow(new ShapeConfig { Position = AnchorPosition.BottomRight, Size = 240, Thickness = 50 });
        }
        else
        {
            AddRow(new ShapeConfig { Position = AnchorPosition.Left, Size = 180, Thickness = 50 });
            AddRow(new ShapeConfig { Position = AnchorPosition.Top, Size = 180, Thickness = 50 });
            AddRow(new ShapeConfig { Position = AnchorPosition.Right, Size = 180, Thickness = 50 });
            AddRow(new ShapeConfig { Position = AnchorPosition.Bottom, Size = 180, Thickness = 50 });
        }

        SetAllFlash(_flashAllCheck.Checked);
    }

    private void PickColorForSelected()
    {
        if (_grid.CurrentRow is not { } row || row.IsNewRow)
        {
            MessageBox.Show(this, "请先选择一行。", "选色", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new ColorDialog();
        var current = row.Cells[_colColor!.Index].Value?.ToString();
        if (!string.IsNullOrWhiteSpace(current))
        {
            try
            {
                dialog.Color = ColorTranslator.FromHtml(current);
            }
            catch
            {
                // ignore invalid color text
            }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            row.Cells[_colColor!.Index].Value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void OnGridDataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        // Never show the default error dialog. If a combo value becomes invalid,
        // reset it to a safe default so the user can keep editing.
        if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count && e.ColumnIndex >= 0 && e.ColumnIndex < _grid.Columns.Count)
        {
            var column = _grid.Columns[e.ColumnIndex];
            if (column == _colPosition)
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = AnchorPosition.TopLeft;
            else if (column == _colShape)
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = ShapeKind.Triangle;
        }

        e.ThrowException = false;
    }

    private void ApplyChanges()
    {
        if (!TryBuildConfig(out var config))
            return;

        if (!TrySaveConfig(config))
            return;

        Applied?.Invoke(config);
    }

    private void SaveAndClose()
    {
        if (!TryBuildConfig(out var config))
            return;

        if (!TrySaveConfig(config))
            return;

        Result = config;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryBuildConfig(out OverlayConfig result)
    {
        var shapes = new List<ShapeConfig>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow)
                continue;

            try
            {
                var position = (AnchorPosition)(row.Cells[_colPosition!.Index].Value ?? AnchorPosition.TopLeft);
                var kind = (ShapeKind)(row.Cells[_colShape!.Index].Value ?? ShapeKind.Triangle);
                int size = Convert.ToInt32(row.Cells[_colSize!.Index].Value ?? 240);
                int thickness = Convert.ToInt32(row.Cells[_colThickness!.Index].Value ?? 50);
                string color = row.Cells[_colColor!.Index].Value?.ToString()?.Trim() ?? "#00FF00";
                bool flashEnabled = row.Cells[_colFlash!.Index].Value is true;
                string color2 = row.Cells[_colColor2!.Index].Value?.ToString()?.Trim() ?? "#FF0000";
                int opacity = Convert.ToInt32(row.Cells[_colOpacity!.Index].Value ?? 140);

                // Validate colors
                var parsed = ColorTranslator.FromHtml(color);
                color = $"#{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}";
                var parsed2 = ColorTranslator.FromHtml(color2);
                color2 = $"#{parsed2.R:X2}{parsed2.G:X2}{parsed2.B:X2}";

                shapes.Add(new ShapeConfig
                {
                    Position = position,
                    Shape = kind,
                    Size = Math.Max(1, size),
                    Thickness = Math.Max(1, thickness),
                    Color = color,
                    FlashEnabled = flashEnabled,
                    Color2 = color2,
                    Opacity = Math.Clamp(opacity, 0, 255)
                });
            }
            catch
            {
                MessageBox.Show(this, "配置有误，请检查每一行的数值和颜色格式。", "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                result = null!;
                return false;
            }
        }

        result = new OverlayConfig
        {
            EdgeInset = (int)_edgeInset.Value,
            FlashIntervalMs = (int)_flashInterval.Value,
            Shapes = shapes
        };
        return true;
    }

    private bool TrySaveConfig(OverlayConfig config)
    {
        try
        {
            config.Save(_configPath);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"保存失败：{ex.Message}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
