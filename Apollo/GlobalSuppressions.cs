// <copyright file="GlobalSuppressions.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1200:Using directives should be placed correctly", Justification = "Disabled to stay with the default Visual Studio style of having using statements outside the namespace.")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:Elements should be documented", Justification = "Element names should be self-documenting, so documentation should be applied at the discretion of the programmer to cover detail when needed.")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "Specifying \"this\" on local calls does not increase readability and creates cluttered code.")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1503:Braces should not be omitted", Justification = "Having no brackets where possible decreases the vertical space occupied by code and can make code more readable when used correctly.")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1122:Use string.Empty for empty strings", Justification = "There is no real difference between string.Empty and \"\" from a programmatic standpoint. \"\" should be used to stay with the conventions of other major programming languages.")]