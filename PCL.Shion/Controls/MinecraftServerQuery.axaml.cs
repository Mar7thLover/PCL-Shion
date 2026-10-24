using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using FluentValidation;

namespace PCL.Shion.Controls;

public partial class MinecraftServerQuery : Grid
{
    public MinecraftServerQuery()
    {
        InitializeComponent();
        LabServerIp.ValidateRules.Add(new BlacklistValidator(["/"]));
        BtnServerQuery.Click += BtnServerQuery_Click;
    }

    private void BtnServerQuery_Click(object? sender, PointerReleasedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _ = ServerQueryAsync());
    }

    private async Task ServerQueryAsync()
    {
        await PanMcServer.UpdateServerInfoAsync(LabServerIp.Text);
        ServerInfo.IsVisible = true;
    }

    private sealed class BlacklistValidator : AbstractValidator<string>
    {
        public BlacklistValidator(List<string> blacklist)
        {
            RuleFor(x => x).Custom((input, context) =>
            {
                foreach (var item in blacklist)
                {
                    if (input.Contains(item))
                    {
                        context.AddFailure($"输入内容不能包含 {item}！");
                    }
                }
            });
        }
    }
}
