using System.Collections;
using Deucarian.Authentication.Editor;
using Deucarian.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationNativeLayoutTests
    {
        [Test]
        public void ReturningPreservesNonSensitiveSessionAndStorageDetails()
        {
            using (var page = AuthenticationWindow.CreatePage())
            {
                page.Activate(null);
                var details = page.Root.Q("authentication-content").Query<Foldout>().ToList();
                Assert.That(details, Is.Not.Empty);
                foreach (var detail in details) detail.value = true;
                page.Deactivate(); page.Activate(null);
                foreach (var detail in details)
                {
                    Assert.That(page.Root.Contains(detail), Is.True, detail.text);
                    Assert.That(detail.value, Is.True, detail.text);
                }
            }
        }

        [UnityTest]
        public IEnumerator SessionHierarchyRemainsReadableAtEverySupportedScale()
        {
            int original = DeucarianEditorAppearance.WorkspaceScalePercent;
            var window = ScriptableObject.CreateInstance<AuthenticationLayoutWindow>();
            window.Show();
            try
            {
                using (var page = AuthenticationWindow.CreatePage())
                {
                    window.rootVisualElement.Add(page.Root); page.Root.style.flexGrow = 1;
                    foreach (float width in new[] { 900f, 1586f })
                    foreach (int scale in new[] { 75, 100, 125, 150 })
                    {
                        window.position = new Rect(50, 50, width, 940);
                        DeucarianEditorAppearance.WorkspaceScalePercent = scale;
                        page.Update(window.position);
                        for (int i = 0; i < 8; i++) yield return null;
                        var panel = page.Root.Q("authentication-session");
                        var scroll = page.Root.Q<ScrollView>("authentication-content");
                        Assert.Greater(scroll.contentViewport.worldBound.height, 100);
                        Assert.LessOrEqual(panel.worldBound.xMax, scroll.contentViewport.worldBound.xMax + 1);
                        var hero = page.Root.Q("authentication-status");
                        Assert.That(hero.ClassListContains("dw-session-hero"), Is.True);
                        Assert.That(page.Root.Query<IMGUIContainer>().ToList(), Is.Empty);
                        Assert.NotNull(page.Root.Q("authentication-session-state"));
                        Assert.NotNull(page.Root.Q("authentication-storage-mode"));
                    }
                }
            }
            finally { window.Close(); DeucarianEditorAppearance.WorkspaceScalePercent = original; }
        }
    }

    internal sealed class AuthenticationLayoutWindow : EditorWindow { }
}
