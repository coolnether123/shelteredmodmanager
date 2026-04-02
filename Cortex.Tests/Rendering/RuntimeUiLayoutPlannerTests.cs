using System;
using Cortex.Core.Models;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi.Panels;
using Cortex.Rendering.RuntimeUi.PopupMenus;
using Cortex.Rendering.RuntimeUi.Tooltips;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public sealed class RuntimeUiLayoutPlannerTests
    {
        [Fact]
        public void PopupMenuLayoutPlanner_ClampsMenuRectToViewport()
        {
            var plan = PopupMenuLayoutPlanner.BuildLayout(
                new RenderPoint(390f, 290f),
                new RenderSize(400f, 300f),
                new[]
                {
                    new PopupMenuItemModel { CommandId = "a", Label = "A", Enabled = true },
                    new PopupMenuItemModel { IsSeparator = true },
                    new PopupMenuItemModel { CommandId = "b", Label = "B", Enabled = true }
                });

            Assert.True(plan.MenuRect.X >= 0f);
            Assert.True(plan.MenuRect.Y >= 0f);
            Assert.True(plan.MenuRect.X + plan.MenuRect.Width <= 400f);
            Assert.True(plan.MenuRect.Y + plan.MenuRect.Height <= 300f);
            Assert.True(plan.ContentHeight > 0f);
        }

        [Fact]
        public void PopupMenuLayoutPlanner_BuildsDrawLayout_ForItemsAndScrollChrome()
        {
            var items = new[]
            {
                new PopupMenuItemModel { CommandId = "a", Label = "A", Enabled = true },
                new PopupMenuItemModel { IsSeparator = true },
                new PopupMenuItemModel { IsSectionHeader = true, Label = "Group" },
                new PopupMenuItemModel { CommandId = "b", Label = "B", Enabled = true, ShortcutText = "Ctrl+B" }
            };

            var drawLayout = PopupMenuLayoutPlanner.BuildDrawLayout(
                new RenderPoint(24f, 18f),
                new RenderSize(320f, 120f),
                items,
                12f,
                "Header");

            Assert.Equal(4, drawLayout.Items.Count);
            Assert.True(drawLayout.HeaderTextRect.Width > 0f);
            Assert.True(drawLayout.ViewportRect.Height > 0f);
            Assert.True(drawLayout.HasScroll);
            Assert.NotNull(drawLayout.ScrollChrome);
            Assert.True(drawLayout.ScrollChrome.ThumbRect.Height > 0f);
        }

        [Fact]
        public void PanelLayoutPlanner_SkipsCollapsedSectionContent()
        {
            var document = new PanelDocument
            {
                Sections = new[]
                {
                    new PanelSection
                    {
                        Id = "expanded",
                        Title = "Expanded",
                        Expanded = true,
                        Elements = new PanelElement[]
                        {
                            new PanelMetadataElement { Label = "Name", Value = "Value" }
                        }
                    },
                    new PanelSection
                    {
                        Id = "collapsed",
                        Title = "Collapsed",
                        Expanded = false,
                        Elements = new PanelElement[]
                        {
                            new PanelMetadataElement { Label = "Ignored", Value = "Ignored" }
                        }
                    }
                }
            };

            var layout = PanelLayoutPlanner.BuildContentLayout(document, 420f, new FixedPanelLayoutMeasurer());

            Assert.Equal(2, layout.Sections.Count);
            Assert.Single(layout.Sections[0].ElementLayouts);
            Assert.Empty(layout.Sections[1].ElementLayouts);
            Assert.True(layout.TotalHeight > PanelLayoutPlanner.SectionHeaderHeight);
        }

        [Fact]
        public void PanelLayoutPlanner_BuildsPortableHeaderAndElementSubLayouts()
        {
            var rootLayout = PanelLayoutPlanner.BuildRootLayout(
                new RenderRect(0f, 0f, 480f, 360f),
                new[]
                {
                    new PanelAction { Id = "one", Label = "One" },
                    new PanelAction { Id = "two", Label = "Two" }
                });
            var document = new PanelDocument
            {
                Sections = new[]
                {
                    new PanelSection
                    {
                        Id = "section",
                        Title = "Section",
                        Expanded = true,
                        Elements = new PanelElement[]
                        {
                            new PanelMetadataElement { Label = "Name", Value = "Value", DrawDivider = true },
                            new PanelTextElement { Label = "Body", Value = "Text" },
                            new PanelActionElement { Action = new PanelAction { Id = "apply", Label = "Apply" }, Hint = "Hint" },
                            new PanelCardElement
                            {
                                Title = "Card",
                                Body = "Body",
                                Rows = new[] { new PanelMetadataElement { Label = "R", Value = "V" } },
                                Actions = new[] { new PanelAction { Id = "open", Label = "Open" } }
                            }
                        }
                    }
                }
            };

            var contentLayout = PanelLayoutPlanner.BuildContentLayout(document, 420f, new FixedPanelLayoutMeasurer());

            Assert.Equal(2, rootLayout.HeaderActionButtonRects.Count);
            Assert.True(rootLayout.TitleRect.Width > 0f);
            Assert.True(rootLayout.CloseButtonRect.Width > 0f);
            Assert.NotNull(contentLayout.Sections[0].ElementLayouts[0].MetadataLayout);
            Assert.NotNull(contentLayout.Sections[0].ElementLayouts[1].TextLayout);
            Assert.NotNull(contentLayout.Sections[0].ElementLayouts[2].ActionLayout);
            Assert.NotNull(contentLayout.Sections[0].ElementLayouts[3].CardLayout);
            Assert.True(contentLayout.Sections[0].ElementLayouts[3].CardLayout.ActionRects.Count == 1);
        }

        [Fact]
        public void HoverTooltipLayoutPlanner_BuildsPortableTooltipLayout()
        {
            var plan = HoverTooltipLayoutPlanner.BuildLayout(
                new HoverTooltipRuntimeState(),
                new HoverTooltipRenderModel
                {
                    Key = "alpha",
                    AnchorRect = new RenderRect(12f, 12f, 24f, 18f),
                    QualifiedPath = "Namespace.Type.Member",
                    SummaryText = "summary",
                    DocumentationText = "details",
                    SignatureParts = new[]
                    {
                        new EditorHoverContentPart
                        {
                            Text = "Alpha",
                            IsInteractive = true,
                            NavigationTarget = new EditorHoverNavigationTarget
                            {
                                MetadataName = "Alpha",
                                DefinitionDocumentPath = "Alpha.cs"
                            }
                        },
                        new EditorHoverContentPart { Text = "(int value)" }
                    }
                },
                new RenderPoint(28f, 24f),
                new RenderSize(640f, 480f),
                new RenderSize(640f, 480f),
                true,
                new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc),
                0f,
                420f,
                true,
                new FixedHoverTooltipLayoutMeasurer());

            Assert.True(plan.Visible);
            Assert.True(plan.TooltipRect.Width > 0f);
            Assert.True(plan.PathRect.Width > 0f);
            Assert.True(plan.MetaRect.Height > 0f);
            Assert.True(plan.PartLayouts.Count >= 2);
            Assert.Equal("summary", plan.MetaText);
            Assert.Contains("details", plan.DetailText);
        }

        private sealed class FixedPanelLayoutMeasurer : IPanelLayoutMeasurer
        {
            public float MeasureMetadataHeight(float width, PanelMetadataElement element)
            {
                return 18f;
            }

            public float MeasureTextHeight(float width, PanelTextElement element)
            {
                return 24f;
            }

            public float MeasureActionHeight(float width, PanelActionElement element)
            {
                return 34f;
            }

            public float MeasureCardHeight(float width, PanelCardElement element)
            {
                return 52f;
            }
        }

        private sealed class FixedHoverTooltipLayoutMeasurer : IHoverTooltipLayoutMeasurer
        {
            public float MeasurePartWidth(EditorHoverContentPart part)
            {
                var text = part != null ? part.Text ?? string.Empty : string.Empty;
                return text.Length * 8f;
            }

            public float MeasurePathHeight(string text, float width)
            {
                return string.IsNullOrEmpty(text) ? 0f : 16f;
            }

            public float MeasureMetaHeight(string text, float width)
            {
                return string.IsNullOrEmpty(text) ? 0f : 16f;
            }

            public float MeasureDetailHeight(string text, float width)
            {
                return string.IsNullOrEmpty(text) ? 0f : 24f;
            }

            public float MeasureLineHeight()
            {
                return 20f;
            }
        }
    }
}
