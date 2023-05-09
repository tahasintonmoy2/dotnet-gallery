// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.WindowsAppSDK.Runtime.Packages.DDLM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT;
using System.Runtime.InteropServices; // For DllImport
using Microsoft.UI.Composition.SystemBackdrops;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUi
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        ImageRepo ImageRepo { get; } = new();
        public MainWindow()
        {
            this.InitializeComponent();
            TrySetMicaBackdrop();

            // no UIElement is set for titlebar, fallback titlebar is created
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);  // this line is optional as by it is null by default

            string folderPath = "C:\\Users\\Tahasin\\Documents\\Images";
            LoadImages(folderPath);
        }

        private void LoadImages(string folderPath)
        {
            ImageRepo.GetImages(folderPath);
            var numImage = ImageRepo.Images.Count;
            ImageInfoBar.Message = $"{numImage} have loaded";
            ImageInfoBar.IsOpen = true;
        }

        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
           FolderPicker folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");

           var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();

            if(folder != null)
            {
                LoadImages(folder.Path);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var ImageInfo = ((sender as Button)?.DataContext as Imageinfo);
            
            if(ImageInfo != null)
            {
                Image image = new();
                image.Source = new BitmapImage(new Uri(ImageInfo.FullName, UriKind.Absolute));
                Window window = new Window()
                {
                    Title = ImageInfo.Name,
                    Content = image,
                
                };
                SetWindowSize(window, 640, 580);

                window.Activate();
            }
        }

        private static void SetWindowSize(Window window, int height, int width)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowsId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowsId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }
        //Compositor _compositor = App.CurrentWindow.Compositor;
        private SpringVector3NaturalMotionAnimation _springAnimation;

        private void OnElementPointerEntered(object sender,  PointerRoutedEventArgs e)
        {
            CreateOrUpdateSpringAnimation(1.05f);
            (sender as UIElement)?.StartAnimation(_springAnimation);
        }        
        private void OnElementPointerExited(object sender,  PointerRoutedEventArgs e)
        {
            CreateOrUpdateSpringAnimation(1.0f);
            (sender as UIElement)?.StartAnimation(_springAnimation);
        }
        private void CreateOrUpdateSpringAnimation(float finalValue)
        {
            if (_springAnimation is null)
            {
                Compositor compositor = this.Compositor;
                if(compositor is not null)
                {
                _springAnimation = compositor.CreateSpringVector3Animation();
                _springAnimation.Target = "Scale";
                }
            }
            _springAnimation.FinalValue = new Vector3(finalValue);
        }

    WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See separate sample below for implementation
        Microsoft.UI.Composition.SystemBackdrops.MicaController m_micaController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

    bool TrySetMicaBackdrop()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

            // Hooking up the policy object
            m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
            this.Activated += Window_Activated;
            this.Closed += Window_Closed;
            ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

            // Initial configuration state.
            m_configurationSource.IsInputActive = true;
            SetConfigurationSourceTheme();

            m_micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

            // Enable the system backdrop.
            // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
            m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
            return true; // succeeded
        }

        return false; // Mica is not supported on this system
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
        // use this closed window.
        if (m_micaController != null)
        {
            m_micaController.Dispose();
            m_micaController = null;
        }
        this.Activated -= Window_Activated;
        m_configurationSource = null;
    }

    private void Window_ThemeChanged(FrameworkElement sender, object args)
    {
        if (m_configurationSource != null)
        {
            SetConfigurationSourceTheme();
        }
    }

    private void SetConfigurationSourceTheme()
    {
        switch (((FrameworkElement)this.Content).ActualTheme)
        {
            case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
            case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
            case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
        }
    }

    private void element_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Scale up to 1.5
            CreateOrUpdateSpringAnimation(1.5f);

            (sender as UIElement).StartAnimation(_springAnimation);
        }

        private void element_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Scale back down to 1.0
            CreateOrUpdateSpringAnimation(1.0f);

            (sender as UIElement).StartAnimation(_springAnimation);
        }

        private async void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            ContentDialog contentDialog = new()
            {
                Title= "About",
                Content = "//Build 2023 \n\n This app Build by Tahasin",
                CloseButtonText ="Close",

                XamlRoot = (sender as Button)?.XamlRoot
            };
            await contentDialog.ShowAsync();
        }
    }
    // The ItemsSource used is a list of custom-class Imageinfo objects called Images


    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
    public class Imageinfo
    {
        public Imageinfo(string name, string fullname)
        {
            Name = name;
            FullName = fullname;
        }
        public string Name { get; }
        public string FullName { get; }
    }

    public class ImageRepo
    {
    public ObservableCollection<Imageinfo> Images { get; } = new();

        public void GetImages(string folderPath)
        {
            Images.Clear();

            var di = new DirectoryInfo(folderPath);
            var files = di.GetFiles("*jpg");

            foreach ( var file in files)
            {
                Images.Add(new Imageinfo( file.Name, file.FullName));
            }
        }
    }
}
