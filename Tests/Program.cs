using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PropertyTools.Wpf;
using SpaceEditor.Algorithms;
using SpaceEditor.Controls;
using SpaceEditor.Data;
using SpaceEditor.Services;

// 无窗口检查；不加载游戏，也不读写游戏文件或用户设置。
internal static class Program
{
    private static int checks;

    private static void Check(bool value, string name)
    {
        if (!value)
            throw new Exception("检查失败：" + name);
        checks++;
        Console.WriteLine("通过：" + name);
    }

    [STAThread]
    private static void Main()
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SpaceEditor;component/Localization/Strings.en.xaml", UriKind.Relative)
        });
        app.Resources["LocalizedTypeNameTemplate"] = LocalizedNameConverter.Template("Key");
        var culture = CultureInfo.CurrentCulture.Name;
        var packPath = Path.Combine(AppContext.BaseDirectory, "Localization", "zh-CN.json");
        var originalPack = File.Exists(packPath) ? File.ReadAllText(packPath) : null;
        try
        {
            Check(LocalizationManager.Current.LanguageCode == "en", "默认英文");
            Check(LocalizationManager.Text("MainWindow_Main") == "Main", "原版英文资源");
            Check(LocalizationManager.DisplayName("MouseLeftButton") == "MouseLeftButton", "英文动态名称保持原样");

            var settings = new GridShaper.GeneratorSettings();
            var descriptors = TypeDescriptor.GetProperties(settings);
            var op = new FlatGroupingDataGridOperator();
            var englishLabel = op.CreatePropertyItem(descriptors["SlopesUpper"]!, descriptors, settings).DisplayName;
            var preset = new PresetVM(null!) { Key = "Current" };
            var source = new ObjectSource { Value = settings };
            var reflected = new ReflectedObject();
            BindingOperations.SetBinding(reflected, ReflectedObject.ReflectedInstanceProperty,
                new Binding(nameof(ObjectSource.Value)) { Source = source, Mode = BindingMode.TwoWay });

            LocalizationManager.Current.LanguageCode = "zh-CN";
            Check(LocalizationManager.Text("MainWindow_Main") == "主页", "切换简体中文");
            Check(LocalizationManager.PropertyName("SlopesUpper") == "生成顶部斜坡", "属性中文名称");
            Check(LocalizationManager.DisplayName("MouseLeftButton").Contains("鼠标")
                && LocalizationManager.DisplayName("MouseLeftButton").Contains("MouseLeftButton"), "动态名称附带原文");
            Check(LocalizationManager.DisplayName("QuuxZorg932") == "QuuxZorg932", "未收录名称保持原值");
            Check(ReferenceEquals(source.Value, settings) && ReferenceEquals(reflected.ReflectedInstance, settings),
                "语言切换保留双向绑定对象");
            Check(settings.SlopesUpper && settings.BlockSize == GridShaper.BlockSizes.TwoPointFive,
                "未保存的设置不变");
            var item = op.CreatePropertyItem(descriptors["SlopesUpper"]!, descriptors, settings);
            Check(item.DisplayName == "生成顶部斜坡" && item.Descriptor.Name == "SlopesUpper",
                "属性面板中文显示与原始绑定名");

            Check(preset.DisplayKey == "当前游戏设置" && preset.Key == "Current", "内置预设显示与保存键");
            preset.DisplayKey = "当前游戏设置";
            Check(preset.Key == "Current", "显示刷新不改写内置预设键");
            preset.DisplayKey = "我的配置";
            Check(preset.Key == "我的配置", "内置预设可以另存为新名称");
            preset.DisplayKey = "当前游戏设置";
            Check(preset.Key == "当前游戏设置", "同名中文自定义预设保留原键");

            var converter = new LocalizedNameConverter();
            Check((string)converter.Convert(new object[] { "Current", "zh-CN" }, typeof(string), "Preset", CultureInfo.InvariantCulture)
                == "当前游戏设置", "下拉预设中文显示");
            Check((string)converter.Convert(new object[] { "我的配置", "zh-CN" }, typeof(string), "Preset", CultureInfo.InvariantCulture)
                == "我的配置", "自定义预设名称不翻译");
            Check((string)converter.Convert(new object[] { GridShaper.BlockSizes.TwoPointFive, "zh-CN" },
                typeof(string), "BlockSize", CultureInfo.InvariantCulture) == "2.5 米", "方块尺寸显示翻译");

            var sizeItem = op.CreatePropertyItem(descriptors["BlockSize"]!, descriptors, settings);
            var factory = new CompositePropertyGridControlFactory();
            var sizes = (ComboBox)factory.TryCreateControl(sizeItem, new PropertyControlFactoryOptions())!;
            Check(sizes.Items.Cast<string>().SequenceEqual(new[] { GridShaper.BlockSizes.TwoPointFive, GridShaper.BlockSizes.HalfMeter }),
                "方块尺寸选项保留生成算法使用的值");
            Check(sizes.GetBindingExpression(ComboBox.SelectedItemProperty)!.ParentBinding.Path.Path == "BlockSize",
                "方块尺寸绑定路径保持原样");

            var inputId = new InputId();
            var ids = new InputIds();
            ids.Digitals.Add(inputId);
            ids.InputIdToDisplayName[inputId] = "MouseLeftButton";
            var digital = new DigitalControlBuilder { Input = inputId };
            var inputDescriptors = TypeDescriptor.GetProperties(digital);
            var inputItem = op.CreatePropertyItem(inputDescriptors["Input"]!, inputDescriptors, digital);
            var inputFactory = new InputIdControlsFactory { InputIds = ids };
            var inputList = (ComboBox)inputFactory.TryCreateControl(inputItem, new PropertyControlFactoryOptions())!;
            Check(ReferenceEquals(inputList.Items.Cast<KeyValuePair<object, string>>().Single().Key, inputId)
                && ids.InputIdToDisplayName[inputId] == "MouseLeftButton", "输入下拉列表保留原始 ID 和数据");

            LocalizationManager.Current.LanguageCode = "en";
            Check(op.CreatePropertyItem(descriptors["SlopesUpper"]!, descriptors, settings).DisplayName == englishLabel,
                "切回英文恢复原版属性标签");
            Check(preset.Key == "当前游戏设置", "切回英文不改写自定义名称");
            Check(CultureInfo.CurrentCulture.Name == culture, "不改变序列化区域设置");

            Directory.CreateDirectory(Path.GetDirectoryName(packPath)!);
            File.WriteAllText(packPath, "{\"strings\":{\"MainWindow_Main\":\"中文补丁生效\"},\"names\":{}}");
            LocalizationManager.Current.LanguageCode = "zh-CN";
            Check(LocalizationManager.Text("MainWindow_Main") == "中文补丁生效", "替换外部 JSON 即可更新翻译");
            Check(LocalizationManager.Text("KeyBindsTab") == "Key Binds", "语言包缺少新文案时回退英文");

            LocalizationManager.Current.LanguageCode = "en";
            File.WriteAllText(packPath, "invalid JSON");
            LocalizationManager.Current.LanguageCode = "zh-CN";
            Check(LocalizationManager.Text("MainWindow_Main") == "主页", "无效语言包回退内置翻译");
            LocalizationManager.Current.LanguageCode = "en";
            File.Delete(packPath);
            LocalizationManager.Current.LanguageCode = "zh-CN";
            Check(LocalizationManager.Text("MainWindow_Main") == "主页", "语言包缺失时回退内置翻译");
            LocalizationManager.Current.LanguageCode = "unsupported";
            Check(LocalizationManager.Current.LanguageCode == "zh-CN", "忽略未支持的语言代码");
            Console.WriteLine($"共 {checks} 项检查通过。");
        }
        finally
        {
            LocalizationManager.Current.LanguageCode = "en";
            if (originalPack is not null)
                File.WriteAllText(packPath, originalPack);
            else if (File.Exists(packPath))
                File.Delete(packPath);
        }
    }

    public sealed class ObjectSource { public object? Value { get; set; } }
    public sealed class InputId { }
    public sealed class DigitalControlBuilder { public InputId Input { get; set; } = null!; }
}
