// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Silicons.Laws;
using Content.Shared.Random;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Dumont.Silicons;

[TestFixture]
public sealed class IonStormTest
{
    private static readonly ProtoId<WeightedRandomPrototype> IonLawTemplates = "DumontIonStormLaws";

    [Test]
    public async Task GeneratedLawsAreLocalized()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var systems = server.ResolveDependency<IEntitySystemManager>();
            var ion = systems.GetEntitySystem<IonStormSystem>();
            var table = prototypes.Index(IonLawTemplates);
            var weights = table.Weights.ToArray();

            try
            {
                foreach (var (template, _) in weights)
                {
                    table.Weights.Clear();
                    table.Weights.Add(template, 1);

                    var samples = template == "Default" ? 512 : 1;
                    for (var i = 0; i < samples; i++)
                    {
                        var law = ion.GenerateLaw();
                        Assert.That(law, Is.Not.Empty, template);
                        Assert.That(law, Does.Not.StartWith("ion-storm-"), template);
                        Assert.That(law, Does.Not.Contain("{$"), template);
                        Assert.That(law, Does.Not.StartWith("THE SHUTTLE MUST BE CALLED"));
                        Assert.That(law, Does.Not.StartWith("O TRANSPORTE DEVE SER CHAMADO"));
                    }
                }
            }
            finally
            {
                table.Weights.Clear();
                foreach (var (template, weight) in weights)
                    table.Weights.Add(template, weight);
            }
        });

        await pair.CleanReturnAsync();
    }
}
