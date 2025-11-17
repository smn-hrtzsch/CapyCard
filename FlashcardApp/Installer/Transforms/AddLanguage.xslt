<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">
  <xsl:output method="xml" indent="yes" encoding="utf-8" />

  <!-- Default identity transform copies everything verbatim -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!-- Add a default language to harvested TTF files (WiX v4 uses DefaultLanguage) -->
  <xsl:template match="wix:File[contains(translate(@Source,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), '.ttf') and not(@DefaultLanguage)]">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:attribute name="DefaultLanguage">1031</xsl:attribute>
      <xsl:apply-templates select="node()" />
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>
