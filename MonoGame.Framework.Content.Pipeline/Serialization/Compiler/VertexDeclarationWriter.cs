// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    [ContentTypeWriter]
    class VertexDeclarationWriter : BuiltInContentWriter<VertexDeclarationContent>
    {
        protected internal override void Write(ContentWriter output, VertexDeclarationContent value)
        {
            // If the vertex stride is not defined, calculate it from the vertex elements
            var stride = value.GetStride();
            output.Write(stride);
            output.Write((uint)value.VertexElements.Count);
            foreach (var element in value.VertexElements)
            {
                output.Write((uint)element.Offset);
                output.Write((int)element.VertexElementFormat);
                output.Write((int)element.VertexElementUsage);
                output.Write((uint)element.UsageIndex);
            }
        }
    }
}
