using System.Windows;
using System.Windows.Input;

namespace SharkiDesktopGuardian;

public partial class TutorialWindow : Window
{
    private readonly (string Title, string Body)[] _steps;
    private int _stepIndex;

    public TutorialWindow(string petName)
    {
        InitializeComponent();
        _steps =
        [
            ("¡Hola! Soy " + petName,
                $"{petName} vigila la CPU, la GPU NVIDIA, la RAM y tus discos, y se queda fijo en una esquina de la pantalla. Este tutorial dura un minuto y siempre puedes volver a abrirlo desde el menú contextual."),
            ("Muévelo y ábrelo",
                "Arrastra con el clic izquierdo para colocarlo donde quieras. Haz doble clic sobre él, o pulsa el botón ▤, para desplegar el panel de rendimiento; un clic en cualquier otro punto de la pantalla lo cierra."),
            ("Órdenes de voz",
                "Pulsa el botón del micrófono y di una orden de la lista segura, por ejemplo «abre el panel» o «qué temperatura tienes». Decir «Sharki» antes es opcional. Ninguna frase fuera de la lista se ejecuta."),
            ("Alertas",
                "Si la temperatura, la carga, la RAM o el espacio en disco superan el umbral configurado, Sharki cambia de animación y avisa una vez; en alerta crítica, sus ojos se ponen rojos."),
            ("Ajustes y modo demostración",
                "Desde el menú contextual (clic derecho) puedes abrir Ajustes para personalizar nombre, tamaño, color y umbrales, o activar el Modo demostración para ver todas las animaciones sin esperar a que ocurran de verdad."),
        ];
        RenderStep();
    }

    private void RenderStep()
    {
        var step = _steps[_stepIndex];
        StepCounterText.Text = $"Paso {_stepIndex + 1} de {_steps.Length}";
        StepTitleText.Text = step.Title;
        StepBodyText.Text = step.Body;
        BackButton.Visibility = _stepIndex == 0 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = _stepIndex == _steps.Length - 1 ? "Entendido" : "Siguiente";
    }

    private void Next_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_stepIndex >= _steps.Length - 1)
        {
            Close();
            return;
        }

        _stepIndex++;
        RenderStep();
    }

    private void Back_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_stepIndex == 0)
        {
            return;
        }

        _stepIndex--;
        RenderStep();
    }

    private void Close_Click(object sender, RoutedEventArgs eventArgs) => Close();

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
