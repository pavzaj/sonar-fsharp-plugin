/*
 * Sonar F# Plugin :: Core
 * Copyright (C) 2015 Jorge Costa and SonarSource
 * dev@sonar.codehaus.org
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */
package org.sonar.plugins.fsharp;

import org.sonar.plugins.fsharp.FSharpSourceCodeColorizer;
import org.junit.Test;
import org.sonar.colorizer.CodeColorizer;

import java.io.File;
import java.io.FileReader;
import java.io.Reader;

import static org.fest.assertions.Assertions.assertThat;

public class FSharpSourceCodeColorizerTest {

  private final FSharpSourceCodeColorizer cobolColorizerFormat = new FSharpSourceCodeColorizer();

  @Test
  public void FSharpToHtml() throws Exception {
    Reader FSharpFile = new FileReader(new File("src/test/resources/FSharpSourceCodeColorizerTest/NUnitFramework.cs"));

    String html = new CodeColorizer(cobolColorizerFormat.getTokenizers()).toHtml(FSharpFile);

    assertThat(html).contains("<style");
    assertThat(html).contains("<table class=\"code\"");
    assertThat(html).contains("</html>");

    assertThat(html).contains("<span class=\"cd\">/// Static methods that implement aspects of the NUnit framework that cut </span>");
    assertThat(html).contains("<span class=\"k\">public</span>");
    assertThat(html).contains("<span class=\"s\">\"NUnit.Framework.IgnoreAttribute\"</span>");
    assertThat(html).contains("<span class=\"j\">#endregion</span>");
    assertThat(html).contains("<span class=\"c\">0</span>");
  }

}
