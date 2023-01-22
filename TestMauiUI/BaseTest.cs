
using CommunityToolkit.Maui.UnitTests.Mocks;
using System.Globalization;

namespace TestMauiUI;

// Base Test, basic idea comes from CommunityToolkit.Maui.UnitTests.BaseTest
public abstract class BaseTest : IDisposable
{
    readonly CultureInfo defaultCulture, defaultUiCulture;

    bool isDisposed;

    protected BaseTest()
    {
        defaultCulture = Thread.CurrentThread.CurrentCulture;
        defaultUiCulture = Thread.CurrentThread.CurrentUICulture;

        DispatcherProvider.SetCurrent(new MockDispatcherProvider());
    }

    ~BaseTest() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (isDisposed)
        {
            return;
        }

        Thread.CurrentThread.CurrentCulture = defaultCulture;
        Thread.CurrentThread.CurrentUICulture = defaultUiCulture;
        
        DispatcherProvider.SetCurrent(null);

        isDisposed = true;
    }
}