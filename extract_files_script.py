#!/usr/bin/env python3
"""
Script pro extrakci souborů z HierarchicalMvvm artefaktu do správné adresářové struktury.

Usage:
    python extract_files.py [--input input_file] [--base-dir output_dir] [--force] [--debug]

Příklady:
    python extract_files.py
    python extract_files.py --input artifact.txt --base-dir ./HierarchicalMvvm
    python extract_files.py --force  # přepíše existující soubory
    python extract_files.py --debug  # zobrazí debug informace
"""

import os
import re
import argparse
from pathlib import Path
from typing import List, Tuple, Dict
import sys

class FileExtractor:
    def __init__(self, base_dir: str = ".", force_overwrite: bool = False, debug: bool = False):
        self.base_dir = Path(base_dir)
        self.force_overwrite = force_overwrite
        self.debug = debug
        self.extracted_files: List[str] = []
        self.skipped_files: List[str] = []
        
    def parse_artifact_content(self, content: str) -> Dict[str, str]:
        """
        Parsuje obsah artefaktu a extrahuje jednotlivé soubory.
        
        Returns:
            Dict[file_path, file_content]
        """
        files = {}
        current_file = None
        current_content = []
        in_file_block = False
        
        lines = content.split('\n')
        
        for i, line in enumerate(lines):
            # Detekce začátku souboru: // File: path/to/file.ext
            file_match = re.match(r'^// File: (.+)$', line.strip())
            if file_match:
                # Uložit předchozí soubor
                if current_file and current_content:
                    files[current_file] = '\n'.join(current_content).strip()
                
                # Začít nový soubor
                current_file = file_match.group(1)
                current_content = []
                in_file_block = True
                continue
            
            # Detekce komentáře s obsahem souboru: /*...*/
            if line.strip().startswith('/*') and in_file_block:
                continue
                
            if line.strip().endswith('*/') and in_file_block:
                continue
            
            # Detekce konce file bloku
            if line.strip().startswith('// ===') or line.strip().startswith('// File:'):
                if current_file and current_content:
                    files[current_file] = '\n'.join(current_content).strip()
                    current_content = []
                
                if not line.strip().startswith('// File:'):
                    in_file_block = False
                    current_file = None
                continue
            
            # Přidat obsah do aktuálního souboru
            if in_file_block and current_file:
                current_content.append(line)
        
        # Uložit poslední soubor
        if current_file and current_content:
            files[current_file] = '\n'.join(current_content).strip()
        
        # Detekce C# souborů podle using/namespace (fallback)
        csharp_files = self.detect_csharp_files(content)
        
        # Merge, ale priorita má explicitní File: definice
        for path, content_data in csharp_files.items():
            if path not in files:  # Přidej jen pokud už není definovaný
                files[path] = content_data
        
        return files
    
    def detect_csharp_files(self, content: str) -> Dict[str, str]:
        """
        Detekuje C# soubory podle using a namespace statements.
        """
        csharp_files = {}
        
        if self.debug:
            print("🔍 DEBUG: Spouštím auto-detekci C# souborů...")
        
        # Split obsah na potenciální bloky podle using a namespace
        blocks = re.split(r'\n(?=(?:using\s+|namespace\s+))', content)
        
        if self.debug:
            print(f"🔍 DEBUG: Nalezeno {len(blocks)} potenciálních bloků")
        
        for i, block in enumerate(blocks):
            block = block.strip()
            if not block:
                continue
                
            if self.debug:
                first_lines = '\n'.join(block.split('\n')[:3])
                print(f"🔍 DEBUG: Blok {i+1}:\n{first_lines}...")
                
            # Detekce C# kódu (musí začínat using nebo namespace a nesmí být zakomentovaný)
            if self.is_csharp_code_block(block):
                if self.debug:
                    print(f"✅ DEBUG: Blok {i+1} identifikován jako C# kód")
                    
                file_info = self.extract_csharp_file_info(block)
                if file_info:
                    file_path, clean_content = file_info
                    csharp_files[file_path] = clean_content
                    
                    if self.debug:
                        print(f"📁 DEBUG: Detekován soubor: {file_path}")
                else:
                    if self.debug:
                        print(f"❌ DEBUG: Nepodařilo se extrahovat info ze souboru")
            else:
                if self.debug:
                    print(f"⏭️  DEBUG: Blok {i+1} přeskočen (není C# kód)")
        
        if self.debug:
            print(f"🎯 DEBUG: Celkem auto-detekováno {len(csharp_files)} C# souborů")
        
        return csharp_files
    
    def is_csharp_code_block(self, block: str) -> bool:
        """
        Zkontroluje, jestli blok obsahuje C# kód.
        """
        lines = block.split('\n')
        
        # Najdi první non-empty, non-comment řádek
        for line in lines:
            line = line.strip()
            if not line:
                continue
            if line.startswith('//') or line.startswith('/*') or line.startswith('*'):
                continue
                
            # Zkontroluj, jestli začíná C# keywords
            if (line.startswith('using ') or 
                line.startswith('namespace ') or
                line.startswith('[') or  # Attributes
                line.startswith('public ') or
                line.startswith('internal ') or
                line.startswith('private ')):
                return True
            break
        
        return False
    
    def extract_csharp_file_info(self, block: str) -> Tuple[str, str] | None:
        """
        Extrahuje informace o C# souboru z bloku kódu.
        
        Returns:
            (file_path, clean_content) or None
        """
        lines = block.split('\n')
        namespace_name = None
        class_name = None
        file_type = "cs"
        
        # Analýza kódu pro určení názvu souboru
        for line in lines:
            stripped = line.strip()
            
            # Přeskoč komentáře
            if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
                continue
            
            # Detekce namespace
            namespace_match = re.match(r'namespace\s+([^\s{]+)', stripped)
            if namespace_match:
                namespace_name = namespace_match.group(1)
                continue
            
            # Detekce třídy, interface, record, enum
            class_matches = [
                re.match(r'.*?(?:public|internal|private)?\s*(?:partial\s+)?(?:class|interface|record|enum|struct)\s+(\w+)', stripped),
                re.match(r'.*?\[Generator\].*?(?:public|internal)?\s*(?:class)\s+(\w+)', stripped),
                re.match(r'.*?(?:public|internal)?\s*(?:static\s+)?(?:class)\s+(\w+)', stripped)
            ]
            
            for match in class_matches:
                if match:
                    class_name = match.group(1)
                    break
            
            # Detekce special případů
            if 'ModelWrapperAttribute' in stripped:
                class_name = 'ModelWrapperAttribute'
            elif '[Generator]' in stripped or 'SourceGenerator' in stripped:
                if not class_name:
                    words = stripped.split()
                    for word in words:
                        if 'Generator' in word and word.endswith('Generator'):
                            class_name = word
                            break
        
        # Pokus o určení cesty k souboru
        if not class_name and not namespace_name:
            return None
        
        # Sestavení cesty
        if namespace_name and class_name:
            # Standardní C# soubor
            
            # Detekce typu projektu podle namespace
            if 'Attributes' in namespace_name:
                base_path = 'src/HierarchicalMvvm.Attributes'
            elif 'Generators' in namespace_name or 'Generator' in namespace_name:
                base_path = 'src/HierarchicalMvvm.Generator'
            elif 'Core' in namespace_name:
                base_path = 'src/HierarchicalMvvm.Core'
            elif 'Demo' in namespace_name:
                if 'Models' in namespace_name:
                    base_path = 'src/HierarchicalMvvm.Demo/Models'
                elif 'ViewModels' in namespace_name:
                    base_path = 'src/HierarchicalMvvm.Demo/ViewModels'
                else:
                    base_path = 'src/HierarchicalMvvm.Demo'
            else:
                # Fallback na namespace strukturu
                namespace_parts = namespace_name.split('.')
                base_path = '/'.join(['src'] + namespace_parts) if len(namespace_parts) > 1 else 'src'
            
            file_path = f"{base_path}/{class_name}.{file_type}"
        
        elif class_name:
            # Jen class name bez namespace
            file_path = f"src/{class_name}.{file_type}"
        else:
            return None
        
        # Vyčisti obsah
        clean_content = self.clean_csharp_content(block)
        
        return (file_path, clean_content)
    
    def clean_csharp_content(self, content: str) -> str:
        """
        Vyčistí C# obsah od artefact komentářů a metadata.
        """
        lines = content.split('\n')
        cleaned_lines = []
        skip_comment_block = False
        
        for line in lines:
            stripped = line.strip()
            
            # Přeskoč artifact header komentáře
            if (stripped.startswith('// ===') or 
                stripped.startswith('// KROK') or
                stripped.startswith('// File:') or
                stripped.startswith('// =====')):
                continue
            
            # Přeskoč comment bloky /*...*/
            if stripped.startswith('/*'):
                skip_comment_block = True
                continue
            if stripped.endswith('*/'):
                skip_comment_block = False
                continue
            if skip_comment_block:
                continue
            
            # Přeskoč prázdné řádky na začátku
            if not cleaned_lines and not stripped:
                continue
                
            cleaned_lines.append(line)
        
        # Odstraň trailing prázdné řádky
        while cleaned_lines and not cleaned_lines[-1].strip():
            cleaned_lines.pop()
        
        return '\n'.join(cleaned_lines)
    
    def extract_project_files(self, content: str) -> Dict[str, str]:
        """
        Alternativní parser pro project soubory (.csproj, .sln).
        """
        project_files = {}
        
        # Hledej .csproj soubory
        csproj_pattern = r'// File: ([^\n]+\.csproj)\s*\n/\*\n(.*?)\n\*/'
        for match in re.finditer(csproj_pattern, content, re.DOTALL):
            file_path = match.group(1)
            file_content = match.group(2).strip()
            project_files[file_path] = file_content
        
        # Hledej .sln soubory
        # sln_pattern = r'// File: ([^\n]+\.sln)\s*\n/\*\s*\n(.*?)\n\*/'
        # for match in re.finditer(sln_pattern, content, re.DOTALL):
        #     file_path = match.group(1)
        #     file_content = match.group(2).strip()
        #     project_files[file_path] = file_content
            
        #  Hledej XAML soubory
        xaml_pattern = r'// File: ([^\n]+\.xaml)\n/\*\s*\n(.*?)\n\*/'
        for match in re.finditer(xaml_pattern, content, re.DOTALL):
            file_path = match.group(1)
            file_content = match.group(2).strip()
            project_files[file_path] = file_content
            
        return project_files
    
    def clean_file_content(self, content: str, file_path: str) -> str:
        """
        Vyčistí obsah souboru od artefact komentářů.
        """
        lines = content.split('\n')
        cleaned_lines = []
        
        for line in lines:
            # Přeskoč artifact komentáře
            if (line.strip().startswith('// ===') or 
                line.strip().startswith('// File:') or
                line.strip().startswith('/*') or
                line.strip().startswith('*/')):
                continue
            
            cleaned_lines.append(line)
        
        result = '\n'.join(cleaned_lines).strip()
        
        # Speciální zpracování pro různé typy souborů
        if file_path.endswith('.csproj') or file_path.endswith('.props') or file_path.endswith('.targets'):
            # Odstraň leading/trailing prázdné řádky z XML
            result = result.strip()
        elif file_path.endswith('.cs'):
            # Pro C# soubory použij specializované čištění
            result = self.clean_csharp_content(result)
        
        return result
    
    def create_file(self, file_path: str, content: str) -> bool:
        """
        Vytvoří soubor na daném místě.
        
        Returns:
            True pokud byl soubor vytvořen, False pokud byl přeskočen
        """
        full_path = self.base_dir / file_path
        
        # Vytvoř adresáře
        full_path.parent.mkdir(parents=True, exist_ok=True)
        
        # Zkontroluj, jestli soubor existuje
        if full_path.exists() and not self.force_overwrite:
            print(f"⚠️  Soubor již existuje: {file_path} (použij --force pro přepsání)")
            self.skipped_files.append(file_path)
            return False
        
        # Vyčisti obsah
        clean_content = self.clean_file_content(content, file_path)
        
        # Zapis soubor
        try:
            with open(full_path, 'w', encoding='utf-8', newline='\n') as f:
                f.write(clean_content)
            
            print(f"✅ Vytvořen: {file_path}")
            self.extracted_files.append(file_path)
            return True
            
        except Exception as e:
            print(f"❌ Chyba při vytváření {file_path}: {e}")
            return False
    
    def extract_all_files(self, artifact_content: str):
        """
        Extrahuje všechny soubory z artefaktu.
        """
        print(f"🚀 Extrahuji soubory do: {self.base_dir.absolute()}")
        print("=" * 60)
        
        # Parsuj obsah
        files = self.parse_artifact_content(artifact_content)
        project_files = self.extract_project_files(artifact_content)
        
        # Spojí oba slovníky
        all_files = {**files, **project_files}
        
        if not all_files:
            print("❌ Nebyly nalezeny žádné soubory k extrakci!")
            return
        
        # Rozdělení na explicitní a auto-detekované
        explicit_files = {}
        auto_detected_files = {}
        
        for file_path, content in all_files.items():
            # Zkontroluj, jestli byl soubor explicitně definován v artefaktu
            if f"// File: {file_path}" in artifact_content:
                explicit_files[file_path] = content
            else:
                auto_detected_files[file_path] = content
        
        print(f"📁 Nalezeno celkem {len(all_files)} souborů:")
        
        if explicit_files:
            print(f"📋 Explicitně definované ({len(explicit_files)}):")
            for file_path in sorted(explicit_files.keys()):
                print(f"   ✓ {file_path}")
        
        if auto_detected_files:
            print(f"🔍 Auto-detekované ({len(auto_detected_files)}):")
            for file_path in sorted(auto_detected_files.keys()):
                print(f"   🤖 {file_path}")
        
        print()
        
        # Vytvoř soubory
        for file_path, content in all_files.items():
            self.create_file(file_path, content)
        
        # Shrnutí
        print("=" * 60)
        print(f"📊 Výsledky:")
        print(f"   ✅ Vytvořeno: {len(self.extracted_files)} souborů")
        print(f"   ⚠️  Přeskočeno: {len(self.skipped_files)} souborů")
        
        if auto_detected_files:
            print(f"   🤖 Auto-detekováno: {len(auto_detected_files)} souborů")
        
        if self.skipped_files:
            print(f"\n📋 Přeskočené soubory:")
            for file_path in self.skipped_files:
                print(f"   • {file_path}")
            print(f"\n💡 Tip: Použij --force pro přepsání existujících souborů")
        
        if auto_detected_files:
            print(f"\n🔍 Auto-detekované soubory:")
            for file_path in sorted(auto_detected_files.keys()):
                print(f"   🤖 {file_path}")
            print(f"\n💡 Auto-detekce funguje podle using/namespace statements")
            print(f"   Pokud je cesta špatná, přidej explicitní // File: komentář")

def main():
    parser = argparse.ArgumentParser(
        description='Extrahuje soubory z HierarchicalMvvm artefaktu',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Příklady použití:
  python extract_files.py
  python extract_files.py --input artifact.txt --base-dir ./MyProject
  python extract_files.py --force
  python extract_files.py --debug
  
Script očekává, že obsah artefaktu bude buď:
1. Vložen přímo do scriptu (jako ARTIFACT_CONTENT konstanta)
2. Načten ze souboru (--input parametr)
3. Načten ze stdin (pipe)
        """
    )
    
    parser.add_argument(
        '--input', '-i',
        type=str,
        help='Cesta k souboru s obsahem artefaktu'
    )
    
    parser.add_argument(
        '--base-dir', '-d',
        type=str,
        default='.',
        help='Základní adresář pro extrakci (default: aktuální adresář)'
    )
    
    parser.add_argument(
        '--force', '-f',
        action='store_true',
        help='Přepsat existující soubory'
    )
    
    parser.add_argument(
        '--debug',
        action='store_true',
        help='Zobrazit debug informace o detekci souborů'
    )
    
    args = parser.parse_args()
    
    # Získej obsah artefaktu
    artifact_content = None
    
    if args.input:
        # Načti ze souboru
        try:
            with open(args.input, 'r', encoding='utf-8') as f:
                artifact_content = f.read()
            print(f"📖 Načten obsah z: {args.input}")
        except Exception as e:
            print(f"❌ Chyba při čtení souboru {args.input}: {e}")
            sys.exit(1)
    
    elif not sys.stdin.isatty():
        # Načti ze stdin (pipe)
        artifact_content = sys.stdin.read()
        print("📖 Načten obsah ze stdin")
    
    else:
        # Použij embedded obsah (pokud je definován)
        try:
            artifact_content = ARTIFACT_CONTENT
            print("📖 Použit vestavěný obsah artefaktu")
        except NameError:
            print("❌ Nebyl nalezen žádný obsah artefaktu!")
            print("💡 Použij --input soubor.txt nebo przekaž obsah přes pipe")
            print("💡 Nebo vlož obsah artefaktu do proměnné ARTIFACT_CONTENT v tomto scriptu")
            sys.exit(1)
    
    if not artifact_content or not artifact_content.strip():
        print("❌ Obsah artefaktu je prázdný!")
        sys.exit(1)
    
    # Extrahuj soubory
    extractor = FileExtractor(
        base_dir=args.base_dir,
        force_overwrite=args.force,
        debug=args.debug
    )
    
    extractor.extract_all_files(artifact_content)

# Konstanta s obsahem artefaktu (volitelné - můžeš sem vložit obsah místo použití --input)
ARTIFACT_CONTENT = """
// Sem můžeš vložit obsah artefaktu přímo, pokud nechceš používat --input parametr

// Nebo použij:
// python extract_files.py --input artifact.txt
"""

if __name__ == '__main__':
    main()