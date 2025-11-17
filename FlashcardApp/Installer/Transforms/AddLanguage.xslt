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

  <!-- Tag harvested .ttf files as fonts to satisfy ICE60 without Language attributes -->
  <xsl:template match="wix:File[contains(translate(@Source,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), '.ttf')]">
    <xsl:variable name="normalizedPath" select="translate(@Source, '\\', '/')" />
    <xsl:variable name="fileName">
      <xsl:call-template name="basename">
        <xsl:with-param name="path" select="$normalizedPath" />
      </xsl:call-template>
    </xsl:variable>
    <xsl:variable name="fontName">
      <xsl:choose>
        <xsl:when test="contains($fileName, '.')">
          <xsl:value-of select="substring-before($fileName, '.')" />
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="$fileName" />
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>

    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
      <xsl:if test="not(wix:Font)">
        <wix:Font Id="{concat(@Id, '_Font')}" Name="{$fontName}" />
      </xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- Helper to get the last segment of a path -->
  <xsl:template name="basename">
    <xsl:param name="path" />
    <xsl:choose>
      <xsl:when test="contains($path, '/')">
        <xsl:call-template name="basename">
          <xsl:with-param name="path" select="substring-after($path, '/')" />
        </xsl:call-template>
      </xsl:when>
      <xsl:otherwise>
        <xsl:value-of select="$path" />
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>
</xsl:stylesheet>
