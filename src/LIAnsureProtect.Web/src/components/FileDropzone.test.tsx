import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";

import { FileDropzone } from "./FileDropzone";

function TestDropzone() {
  const [files, setFiles] = useState<File[]>([]);

  return (
    <FileDropzone
      label="Evidence files"
      description="PDF files only."
      accept=".pdf"
      files={files}
      onFilesChange={setFiles}
    />
  );
}

describe("FileDropzone", () => {
  it("adds selected files and lets the user remove each one", async () => {
    const user = userEvent.setup();
    render(<TestDropzone />);
    const file = new File(["evidence"], "control-evidence.pdf", {
      type: "application/pdf",
    });

    await user.upload(screen.getByLabelText("Evidence files"), file);

    expect(screen.getByText("control-evidence.pdf")).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Remove control-evidence.pdf" }),
    );
    expect(screen.queryByText("control-evidence.pdf")).not.toBeInTheDocument();
  });

  it("accepts supported files dropped onto the selection area", () => {
    render(<TestDropzone />);
    const file = new File(["evidence"], "dropped-evidence.pdf", {
      type: "application/pdf",
    });

    fireEvent.drop(screen.getByText("or drag and drop files here").parentElement!.parentElement!, {
      dataTransfer: { files: [file] },
    });

    expect(screen.getByText("dropped-evidence.pdf")).toBeInTheDocument();
  });

  it("rejects an unsupported dropped file with a readable message", () => {
    render(<TestDropzone />);
    const file = new File(["script"], "unsafe.exe", {
      type: "application/x-msdownload",
    });

    fireEvent.drop(screen.getByText("or drag and drop files here").parentElement!.parentElement!, {
      dataTransfer: { files: [file] },
    });

    expect(screen.queryByText("unsafe.exe")).not.toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("unsupported format");
  });
});
